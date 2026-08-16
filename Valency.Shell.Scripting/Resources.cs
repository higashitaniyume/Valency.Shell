using System.Globalization;
using System.Resources;

namespace Valency.Shell.Scripting;

internal static class Resources
{
    private static readonly ResourceManager Manager = new(
        "Valency.Shell.Scripting.Properties.Resources",
        typeof(Resources).Assembly);

    internal static string SyntaxErrorLocation => Get("SyntaxErrorLocation");
    internal static string UnclosedDoubleQuote => Get("UnclosedDoubleQuote");
    internal static string UnclosedCommandSubstitution => Get("UnclosedCommandSubstitution");
    internal static string UnclosedBacktick => Get("UnclosedBacktick");
    internal static string UnclosedSingleQuote => Get("UnclosedSingleQuote");
    internal static string UnclosedExpressionParen => Get("UnclosedExpressionParen");
    internal static string UnexpectedToken => Get("UnexpectedToken");
    internal static string CannotParseToken => Get("CannotParseToken");
    internal static string FunctionParamNeedsVariable => Get("FunctionParamNeedsVariable");
    internal static string InvalidParameterName => Get("InvalidParameterName");
    internal static string UnknownRedirection => Get("UnknownRedirection");
    internal static string HeredocNotSupported => Get("HeredocNotSupported");
    internal static string RedirectionMissingTarget => Get("RedirectionMissingTarget");
    internal static string ExpectedExpression => Get("ExpectedExpression");
    internal static string ExpectedIdentifier => Get("ExpectedIdentifier");
    internal static string ExpectedToken => Get("ExpectedToken");
    internal static string IncompleteInput => Get("IncompleteInput");
    internal static string PipelineCompoundNotSupported => Get("PipelineCompoundNotSupported");
    internal static string BackgroundOnlySingleCommand => Get("BackgroundOnlySingleCommand");
    internal static string ExpressionEmpty => Get("ExpressionEmpty");
    internal static string TernaryMissingColon => Get("TernaryMissingColon");
    internal static string ExpressionUnexpectedEnd => Get("ExpressionUnexpectedEnd");
    internal static string MissingCloseParen => Get("MissingCloseParen");
    internal static string UnknownIdentifier => Get("UnknownIdentifier");
    internal static string CannotParseExpressionChar => Get("CannotParseExpressionChar");
    internal static string UnclosedString => Get("UnclosedString");
    internal static string VariableNeedsDollar => Get("VariableNeedsDollar");
    internal static string UnclosedBracedVariable => Get("UnclosedBracedVariable");
    internal static string DollarNeedsName => Get("DollarNeedsName");
    internal static string LogLexerTokens => Get("LogLexerTokens");
    internal static string LogParsedStatement => Get("LogParsedStatement");
    internal static string LogCommandExecuted => Get("LogCommandExecuted");
    internal static string LogStatementExecuted => Get("LogStatementExecuted");
    internal static string LogExpressionEvaluated => Get("LogExpressionEvaluated");
    internal static string LogWordExpanded => Get("LogWordExpanded");
    internal static string LogGlobExpanded => Get("LogGlobExpanded");
    internal static string LogVariableAssigned => Get("LogVariableAssigned");
    internal static string LogFunctionDefined => Get("LogFunctionDefined");
    internal static string LogFunctionInvoked => Get("LogFunctionInvoked");
    internal static string LogPipelineExecuted => Get("LogPipelineExecuted");
    internal static string LogRedirectResolved => Get("LogRedirectResolved");
    internal static string LogCommandSubstitution => Get("LogCommandSubstitution");

    private static string Get(string key)
        => Manager.GetString(key, CultureInfo.CurrentCulture) ?? key;
}
