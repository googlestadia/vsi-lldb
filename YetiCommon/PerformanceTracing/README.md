# Performance Tracing
Performance Tracing integrates with Chrome's built-in and public diagnostics tool. 

Logging must be enabled at compile time to use this feature.

1. Enable trace-level logging by setting `TracingEnabled = true` in `DebugEngine.cs` and re-compile.
2. Launch the experimental instance. It has tracing enabled for it.
3. After debugging, locate logs. By default it is `~/AppData/Roaming/GGP/logs/YetiVSI.Trace*`
4. Run `python YetiCommon/PerformanceTracing/process_traces file1 file2 ... > output.json`
5. In Chrome, go to chrome://tracing
6. Load the output.json file

(internal)
