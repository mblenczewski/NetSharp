using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NetSharp.Logging
{
    /// <summary>
    /// Specifies the severity level of a log message.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// The logged message contains some information. Lowest severity.
        /// </summary>
        Info,

        /// <summary>
        /// The logged message contains a warning. Higher severity.
        /// </summary>
        Warn,

        /// <summary>
        /// The logged message contains details about an error. Higher severity.
        /// </summary>
        Error,

        /// <summary>
        /// The logged message contains details about an exception. Highest severity.
        /// </summary>
        Exception
    }

    /// <summary>
    /// A simple logger capable of writing text to a stream.
    /// </summary>
    public struct Logger : IDisposable
    {
        /// <summary>
        /// The stream to which messages will be logged.
        /// </summary>
        private readonly Stream loggingStream;

        /// <summary>
        /// The text writer we will use to log messages to the underlying stream.
        /// </summary>
        private readonly StreamWriter writer;

        /// <summary>
        /// The minimum severity that log messages need to be logged to the underlying stream.
        /// </summary>
        private LogLevel minimumSeverity;

        /// <summary>
        /// Initialises a new instance of the <see cref="Logger"/> struct.
        /// </summary>
        /// <param name="streamToLogTo">The stream that the logger instance should log messages to.</param>
        public Logger(Stream streamToLogTo)
        {
            loggingStream = streamToLogTo;
            writer = new StreamWriter(loggingStream, Encoding.Default) { AutoFlush = true };

            minimumSeverity = LogLevel.Info;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            loggingStream.Dispose();
            writer.Dispose();
        }

        /// <summary>
        /// Logs a message to the underlying stream, along with the given exception and at the given severity.
        /// </summary>
        /// <param name="message">The message that should be logged.</param>
        /// <param name="exception">The exception that occurred (if any).</param>
        /// <param name="severity">The severity of the message that is being logged.</param>
        public void Log(string message, Exception? exception, LogLevel severity)
        {
            if (severity < minimumSeverity) return;

            string severityTag = severity switch
            {
                LogLevel.Info => "Info ",
                LogLevel.Warn => "Warn ",
                LogLevel.Error => "Error",
                LogLevel.Exception => "Excep",
                _ => "Info "
            };

            writer.WriteLine($"[{severityTag}] {message} {exception}");
        }

        /// <summary>
        /// Logs a message asynchronously to the underlying stream, along with the given exception and at the given severity.
        /// </summary>
        /// <param name="message">The message that should be logged.</param>
        /// <param name="exception">The exception that occurred (if any).</param>
        /// <param name="severity">The severity of the message that is being logged.</param>
        public async Task LogAsync(string message, Exception? exception, LogLevel severity)
        {
            if (loggingStream.Equals(Stream.Null))
            {
                // ignore log request if the underlying stream is null
                return;
            }

            if (!exception?.Equals(default) ?? false)
            {
                severity = LogLevel.Exception;
            }

            if (severity >= minimumSeverity)
            {
                string severityTag = severity switch
                {
                    LogLevel.Info => "Info ",
                    LogLevel.Warn => "Warn ",
                    LogLevel.Error => "Error",
                    LogLevel.Exception => "Excep",
                    _ => "Info "
                };

                await writer.WriteLineAsync($"[{severityTag}] {message} {exception}");
            }
        }

        /// <summary>
        /// Logs an error to the underlying stream, with severity <see cref="LogLevel.Info"/>.
        /// </summary>
        /// <param name="message">The error that should be logged.</param>
        public void LogError(string message) => Log(message, null, LogLevel.Error);

        /// <summary>
        /// Logs an error to the underlying stream asynchronously, with severity <see cref="LogLevel.Error"/>.
        /// </summary>
        /// <param name="message">The error that should be logged.</param>
        public async Task LogErrorAsync(string message) => await LogAsync(message, null, LogLevel.Error);

        /// <summary>
        /// Logs an exception to the underlying stream, with severity <see cref="LogLevel.Exception"/>.
        /// </summary>
        /// <param name="exception">The exception that should be logged.</param>
        public void LogException(Exception exception) => Log("", exception, LogLevel.Exception);

        /// <summary>
        /// Logs an exception to the underlying stream, along with a short debug message, with severity
        /// <see cref="LogLevel.Exception"/>.
        /// </summary>
        /// <param name="message">The debug message that should be logged with the exception.</param>
        /// <param name="exception">The exception that should be logged.</param>
        public void LogException(string message, Exception exception) => Log(message, exception, LogLevel.Exception);

        /// <summary>
        /// Logs an exception to the underlying stream asynchronously, with severity <see cref="LogLevel.Exception"/>.
        /// </summary>
        /// <param name="exception">The exception that should be logged.</param>
        public async Task LogExceptionAsync(Exception exception) => await LogAsync("", exception, LogLevel.Exception);

        /// <summary>
        /// Logs an exception to the underlying stream asynchronously, along with a short debug message, with severity
        /// <see cref="LogLevel.Exception"/>.
        /// </summary>
        /// <param name="message">The debug message that should be logged with the exception.</param>
        /// <param name="exception">The exception that should be logged.</param>
        public async Task LogExceptionAsync(string message, Exception exception) => await LogAsync(message, exception, LogLevel.Exception);

        /// <summary>
        /// Logs a message to the underlying stream, with severity <see cref="LogLevel.Info"/>.
        /// </summary>
        /// <param name="message">The message that should be logged.</param>
        public void LogMessage(string message) => Log(message, null, LogLevel.Info);

        /// <summary>
        /// Logs a message to the underlying stream asynchronously, with severity <see cref="LogLevel.Info"/>.
        /// </summary>
        /// <param name="message">The message that should be logged.</param>
        public async Task LogMessageAsync(string message) => await LogAsync(message, null, LogLevel.Info);

        /// <summary>
        /// Logs a warning to the underlying stream, with severity <see cref="LogLevel.Info"/>.
        /// </summary>
        /// <param name="message">The warning that should be logged.</param>
        public void LogWarning(string message) => Log(message, null, LogLevel.Warn);

        /// <summary>
        /// Logs a warning to the underlying stream asynchronously, with severity <see cref="LogLevel.Warn"/>.
        /// </summary>
        /// <param name="message">The warning that should be logged.</param>
        public async Task LogWarningAsync(string message) => await LogAsync(message, null, LogLevel.Warn);

        /// <summary>
        /// Sets the minimum severity level that new messages need to be logged to the underlying stream.
        /// </summary>
        /// <param name="minimumSeverityLevel">The new minimum severity level.</param>
        public void SetMinimumLogSeverity(LogLevel minimumSeverityLevel) => minimumSeverity = minimumSeverityLevel;
    }
}