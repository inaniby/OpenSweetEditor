package com.nanib.pydroid.python;

public final class PythonBridge {
    private PythonBridge() {
    }

    public static native int startRuntime(
            String libPythonPath,
            String pythonHome,
            String workingDir,
            String pythonPath,
            String[] extraEnvKeys,
            String[] extraEnvValues
    );

    public static native int stopRuntime();

    public static native boolean isRuntimeStarted();

    public static native int requestInterrupt();

    public static native int runFile(String scriptPath, String[] args);

    public static native int runModule(String module, String[] args);

    public static native int runReplSnippet(String snippet);
}
