using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// 处理代码执行的外部事件处理器
    /// </summary>
    public class ExecuteCodeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public const string TransactionModeAuto = "auto";
        public const string TransactionModeNone = "none";

        // 代码执行参数
        private string _generatedCode;
        private object[] _executionParameters;
        private string _transactionMode = TransactionModeAuto;

        // 执行结果信息
        public ExecutionResultInfo ResultInfo { get; private set; }

        // 状态同步对象
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // 设置要执行的代码和参数
        public void SetExecutionParameters(string code, object[] parameters = null, string transactionMode = TransactionModeAuto)
        {
            _generatedCode = code;
            _executionParameters = parameters ?? Array.Empty<object>();
            _transactionMode = transactionMode == TransactionModeNone ? TransactionModeNone : TransactionModeAuto;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        // 等待执行完成 - IWaitableExternalEventHandler接口实现
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                // Set first: the catch below reports through ResultInfo.
                ResultInfo = new ExecutionResultInfo();

                var doc = app.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No model is open in Revit. Open a project, then try again.");

                object result;
                if (_transactionMode == TransactionModeNone)
                {
                    result = CompileAndExecuteCode(
                        code: _generatedCode,
                        doc: doc,
                        parameters: _executionParameters
                    );
                }
                else
                {
                    using (var transaction = new Transaction(doc, "执行AI代码"))
                    {
                        transaction.Start();

                        result = CompileAndExecuteCode(
                            code: _generatedCode,
                            doc: doc,
                            parameters: _executionParameters
                        );

                        transaction.Commit();
                    }
                }

                ResultInfo.Success = true;
                ResultInfo.Result = ToJson(result);
            }
            catch (Exception ex)
            {
                // Reflection wraps exceptions thrown by the snippet; report the snippet's own exception.
                ResultInfo.Success = false;
                ResultInfo.ErrorMessage = ex is TargetInvocationException { InnerException: { } inner }
                    ? $"The snippet threw {inner.GetType().Name}: {inner.Message}"
                    : ex.Message;
                if (_transactionMode != TransactionModeNone)
                {
                    ResultInfo.ErrorMessage += " (the transaction was rolled back, so the model is unchanged)";
                }
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private object CompileAndExecuteCode(string code, Document doc, object[] parameters)
        {
            // 包装代码以规范入口点
            // The #line directives make compiler errors refer to lines of the snippet, not of this template.
            var wrappedCode = $@"
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace AIGeneratedCode
{{
    public static class CodeExecutor
    {{
        public static object Execute(Document document, object[] parameters)
        {{
            // 用户代码入口
#line 1 ""snippet""
{code}
#line default
        }}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            var references = GetLoadedAssemblyReferences();

            // 编译代码
            var compilation = CSharpCompilation.Create(
                "AIGeneratedCode",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                // 处理编译结果
                if (!result.Success)
                {
                    var errors = string.Join("\n", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(DescribeCompileError));
                    throw new Exception($"Compilation failed:\n{errors}");
                }

                // 反射调用执行方法
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                var executorType = assembly.GetType("AIGeneratedCode.CodeExecutor");
                var executeMethod = executorType.GetMethod("Execute");

                return executeMethod.Invoke(null, new object[] { doc, parameters });
            }
        }

        /// <summary>
        /// References every assembly loaded in Revit, so snippets can use any API another add-in brought in.
        /// Two kinds of assembly would otherwise fail the whole compilation with "Line 0" errors:
        /// a second copy of an already-referenced name (e.g. the BIM 360 add-in's own Autodesk.Http, issue #26),
        /// and files with no readable metadata (protected or obfuscated add-in DLLs, issue #59).
        /// </summary>
        private static List<MetadataReference> GetLoadedAssemblyReferences()
        {
            var references = new List<MetadataReference>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                    continue;

                // The first copy loaded wins, matching which one the CLR actually binds to.
                string name = assembly.GetName().Name;
                if (name == null || seenNames.Contains(name))
                    continue;

                try
                {
                    // CreateFromFile reads metadata lazily, so force the read here to catch unreadable files.
                    var metadata = AssemblyMetadata.CreateFromFile(assembly.Location);
                    metadata.GetModules();
                    references.Add(metadata.GetReference(filePath: assembly.Location));
                    seenNames.Add(name);
                }
                catch (Exception ex) when (ex is BadImageFormatException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.WriteLine($"send_code_to_revit: skipped unreadable assembly {assembly.Location}: {ex.Message}");
                }
            }

            return references;
        }

        /// <summary>
        /// Returns the snippet's result as JSON, once. Revit objects such as Elements can't be serialized
        /// (and by now any changes are committed), so those fall back to their string form instead of failing.
        /// </summary>
        private static JToken ToJson(object result)
        {
            if (result == null)
            {
                return JValue.CreateNull();
            }

            try
            {
                return JToken.FromObject(result);
            }
            catch (Exception)
            {
                return new JValue(result.ToString());
            }
        }

        private static string DescribeCompileError(Diagnostic diagnostic)
        {
            FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
            if (span.HasMappedPath)
            {
                return $"Line {span.StartLinePosition.Line + 1}: {diagnostic.GetMessage()}";
            }

            // Errors outside the snippet come from the template; the usual one is a missing return statement.
            string hint = diagnostic.Id == "CS0161" ? " The snippet must end with a return statement, e.g. 'return null;'." : "";
            return $"Template: {diagnostic.GetMessage()}{hint}";
        }

        public string GetName()
        {
            return "执行AI代码";
        }
    }

    // 执行结果数据结构
    public class ExecutionResultInfo
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("result")]
        public JToken Result { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
