package com.nanib.pydroid.python;

import android.content.Context;
import android.net.Uri;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.documentfile.provider.DocumentFile;

import org.apache.commons.compress.archivers.tar.TarArchiveEntry;
import org.apache.commons.compress.archivers.tar.TarArchiveInputStream;

import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Comparator;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.zip.GZIPInputStream;

public class PythonPackageManager {
    public static final String DEFAULT_SLOT = "current";
    private static final int MAX_PREFIX_SEARCH_DEPTH = 8;
    private static final Pattern PYTHON_DIR_PATTERN = Pattern.compile("python\\d+\\.\\d+[a-z]?");
    private static final Pattern ANDROID_SYSCONFIG_PATTERN =
            Pattern.compile("_sysconfigdata_[A-Za-z0-9_]*_android_(.+)\\.py");

    private final Context mContext;

    public PythonPackageManager(@NonNull Context context) {
        mContext = context.getApplicationContext();
    }

    public static final class InstallResult {
        @NonNull
        public final File installRoot;
        @NonNull
        public final File prefixDir;
        @NonNull
        public final String pythonVersion;
        @NonNull
        public final String abi;

        InstallResult(@NonNull File installRoot,
                      @NonNull File prefixDir,
                      @NonNull String pythonVersion,
                      @NonNull String abi) {
            this.installRoot = installRoot;
            this.prefixDir = prefixDir;
            this.pythonVersion = pythonVersion;
            this.abi = abi;
        }
    }

    private static final class LinkEntry {
        @NonNull
        final File output;
        @NonNull
        final String linkName;

        LinkEntry(@NonNull File output, @NonNull String linkName) {
            this.output = output;
            this.linkName = linkName;
        }
    }

    @NonNull
    public InstallResult installFromOfficialTarGz(@NonNull Uri uri) throws IOException {
        return installFromOfficialTarGz(uri, DEFAULT_SLOT);
    }

    @NonNull
    public InstallResult installFromOfficialTarGz(@NonNull Uri uri, @NonNull String slot) throws IOException {
        File installRoot = prepareInstallRoot(slot);

        InputStream raw = mContext.getContentResolver().openInputStream(uri);
        if (raw == null) {
            throw new IOException("Failed to read package URI: " + uri);
        }

        try (InputStream in = raw) {
            extractTarGz(in, installRoot);
        }

        return analyzeInstalledPackage(installRoot);
    }

    @NonNull
    public InstallResult installFromExtractedTree(@NonNull Uri treeUri) throws IOException {
        return installFromExtractedTree(treeUri, DEFAULT_SLOT);
    }

    @NonNull
    public InstallResult installFromExtractedTree(@NonNull Uri treeUri,
                                                  @NonNull String slot) throws IOException {
        File installRoot = prepareInstallRoot(slot);

        DocumentFile rootDocument = DocumentFile.fromTreeUri(mContext, treeUri);
        if (rootDocument == null) {
            throw new IOException("Failed to open selected directory");
        }
        if (!rootDocument.isDirectory()) {
            throw new IOException("Selected URI is not a directory");
        }

        copyDocumentTree(rootDocument, installRoot);
        return analyzeInstalledPackage(installRoot);
    }

    @Nullable
    public InstallResult restoreInstalledPackage() {
        return restoreInstalledPackage(DEFAULT_SLOT);
    }

    @Nullable
    public InstallResult restoreInstalledPackage(@NonNull String slot) {
        File installRoot = new File(mContext.getFilesDir(), "python/runtime/" + slot);
        if (!installRoot.isDirectory()) {
            return null;
        }
        File[] files = installRoot.listFiles();
        if (files == null || files.length == 0) {
            return null;
        }
        try {
            return analyzeInstalledPackage(installRoot);
        } catch (Throwable ignored) {
            return null;
        }
    }

    @NonNull
    private File prepareInstallRoot(@NonNull String slot) throws IOException {
        File installRoot = new File(mContext.getFilesDir(), "python/runtime/" + slot);
        deleteRecursively(installRoot);
        if (!installRoot.mkdirs()) {
            throw new IOException("Failed to create directory: " + installRoot);
        }
        return installRoot;
    }

    @NonNull
    private InstallResult analyzeInstalledPackage(@NonNull File installRoot) throws IOException {
        File prefixDir = locatePrefixDir(installRoot);
        String pythonVersion = detectPythonVersion(prefixDir);
        String abi = detectAbi(prefixDir, pythonVersion);
        ensureSharedObjectPermissions(prefixDir);

        return new InstallResult(installRoot, prefixDir, pythonVersion, abi);
    }

    @NonNull
    private File locatePrefixDir(@NonNull File installRoot) throws IOException {
        List<File> candidates = new ArrayList<>();
        collectPrefixCandidates(installRoot, installRoot, 0, candidates);

        if (candidates.isEmpty()) {
            throw new IOException("No valid prefix directory found in package");
        }

        candidates.sort((a, b) -> {
            int aPrefix = "prefix".equals(a.getName()) ? 0 : 1;
            int bPrefix = "prefix".equals(b.getName()) ? 0 : 1;
            if (aPrefix != bPrefix) {
                return Integer.compare(aPrefix, bPrefix);
            }
            int aDepth = relativeDepth(installRoot, a);
            int bDepth = relativeDepth(installRoot, b);
            if (aDepth != bDepth) {
                return Integer.compare(aDepth, bDepth);
            }
            return a.getAbsolutePath().compareToIgnoreCase(b.getAbsolutePath());
        });

        return candidates.get(0);
    }

    private void collectPrefixCandidates(@NonNull File root,
                                         @NonNull File current,
                                         int depth,
                                         @NonNull List<File> out) {
        if (depth > MAX_PREFIX_SEARCH_DEPTH || !current.isDirectory()) {
            return;
        }

        File libDir = new File(current, "lib");
        File includeDir = new File(current, "include");
        if (libDir.isDirectory() && includeDir.isDirectory()) {
            out.add(current);
        }

        File[] children = current.listFiles();
        if (children == null) {
            return;
        }
        for (File child : children) {
            if (child != null && child.isDirectory()) {
                collectPrefixCandidates(root, child, depth + 1, out);
            }
        }
    }

    private int relativeDepth(@NonNull File root, @NonNull File node) {
        String rootPath = root.getAbsolutePath();
        String nodePath = node.getAbsolutePath();
        if (!nodePath.startsWith(rootPath)) {
            return Integer.MAX_VALUE;
        }
        String relative = nodePath.substring(rootPath.length());
        if (relative.isEmpty()) {
            return 0;
        }
        int depth = 0;
        for (int i = 0; i < relative.length(); i++) {
            if (relative.charAt(i) == File.separatorChar) {
                depth++;
            }
        }
        return depth;
    }

    @NonNull
    private String detectPythonVersion(@NonNull File prefixDir) throws IOException {
        File libDir = new File(prefixDir, "lib");
        File[] children = libDir.listFiles();
        if (children == null || children.length == 0) {
            throw new IOException("Could not find stdlib directory under prefix/lib");
        }
        Arrays.sort(children, Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER));
        for (File child : children) {
            if (child.isDirectory() && PYTHON_DIR_PATTERN.matcher(child.getName()).matches()) {
                return child.getName().substring("python".length());
            }
        }
        throw new IOException("Could not find pythonX.Y stdlib directory");
    }

    @NonNull
    private String detectAbi(@NonNull File prefixDir, @NonNull String version) throws IOException {
        File stdlibDir = new File(prefixDir, "lib/python" + version);
        File[] children = stdlibDir.listFiles();
        if (children == null || children.length == 0) {
            throw new IOException("Could not inspect stdlib directory: " + stdlibDir);
        }

        Arrays.sort(children, Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER));
        for (File child : children) {
            if (!child.isFile()) {
                continue;
            }
            Matcher matcher = ANDROID_SYSCONFIG_PATTERN.matcher(child.getName());
            if (!matcher.matches()) {
                continue;
            }
            String triplet = matcher.group(1);
            if ("aarch64-linux-android".equals(triplet)) {
                return "arm64-v8a";
            }
            if ("x86_64-linux-android".equals(triplet)) {
                return "x86_64";
            }
            throw new IOException("Unsupported ABI triplet: " + triplet);
        }

        throw new IOException("Could not find Android sysconfig data");
    }

    private void extractTarGz(@NonNull InputStream input,
                              @NonNull File targetDir) throws IOException {
        File canonicalTargetRoot = targetDir.getCanonicalFile();
        List<LinkEntry> links = new ArrayList<>();

        try (TarArchiveInputStream tar = new TarArchiveInputStream(
                new GZIPInputStream(new BufferedInputStream(input)))) {
            TarArchiveEntry entry;
            while ((entry = tar.getNextTarEntry()) != null) {
                File outFile = new File(targetDir, entry.getName()).getCanonicalFile();
                if (!isWithinRoot(outFile, canonicalTargetRoot)) {
                    throw new SecurityException("Blocked unsafe archive path: " + entry.getName());
                }

                if (entry.isDirectory()) {
                    if (!outFile.exists() && !outFile.mkdirs()) {
                        throw new IOException("Failed to create directory: " + outFile);
                    }
                    continue;
                }

                if (entry.isSymbolicLink() || entry.isLink()) {
                    links.add(new LinkEntry(outFile, entry.getLinkName()));
                    continue;
                }

                File parent = outFile.getParentFile();
                if (parent != null && !parent.exists() && !parent.mkdirs()) {
                    throw new IOException("Failed to create directory: " + parent);
                }

                try (FileOutputStream fos = new FileOutputStream(outFile)) {
                    copy(tar, fos);
                }
            }
        }

        for (LinkEntry link : links) {
            File parent = link.output.getParentFile();
            if (parent == null) {
                throw new IOException("Invalid link output path: " + link.output);
            }
            if (!parent.exists() && !parent.mkdirs()) {
                throw new IOException("Failed to create directory: " + parent);
            }

            File linked = new File(parent, link.linkName).getCanonicalFile();
            if (!isWithinRoot(linked, canonicalTargetRoot)) {
                throw new SecurityException("Blocked unsafe link target: " + link.linkName);
            }
            if (!linked.isFile()) {
                throw new IOException("Link target not found: " + link.linkName);
            }

            try (InputStream linkInput = new java.io.FileInputStream(linked);
                 FileOutputStream output = new FileOutputStream(link.output)) {
                copy(linkInput, output);
            }
        }
    }

    private void copyDocumentTree(@NonNull DocumentFile rootDocument,
                                  @NonNull File targetDir) throws IOException {
        File canonicalTargetRoot = targetDir.getCanonicalFile();
        DocumentFile[] children = rootDocument.listFiles();
        if (children == null || children.length == 0) {
            throw new IOException("Selected directory is empty");
        }

        for (DocumentFile child : children) {
            copyDocumentNode(child, targetDir, canonicalTargetRoot);
        }
    }

    private void copyDocumentNode(@NonNull DocumentFile node,
                                  @NonNull File parentDir,
                                  @NonNull File canonicalTargetRoot) throws IOException {
        String name = node.getName();
        if (name == null || name.trim().isEmpty()) {
            throw new IOException("Found document without name");
        }

        File out = resolveSafeChild(parentDir, name, canonicalTargetRoot);

        if (node.isDirectory()) {
            if (!out.exists() && !out.mkdirs()) {
                throw new IOException("Failed to create directory: " + out);
            }
            DocumentFile[] children = node.listFiles();
            if (children != null) {
                for (DocumentFile child : children) {
                    copyDocumentNode(child, out, canonicalTargetRoot);
                }
            }
            return;
        }

        if (!node.isFile()) {
            return;
        }

        File parent = out.getParentFile();
        if (parent == null) {
            throw new IOException("Invalid output file path: " + out);
        }
        if (!parent.exists() && !parent.mkdirs()) {
            throw new IOException("Failed to create directory: " + parent);
        }

        InputStream input = mContext.getContentResolver().openInputStream(node.getUri());
        if (input == null) {
            throw new IOException("Failed to open file in selected directory: " + name);
        }

        try (InputStream in = input;
             FileOutputStream output = new FileOutputStream(out)) {
            copy(in, output);
        }
    }

    @NonNull
    private File resolveSafeChild(@NonNull File parentDir,
                                  @NonNull String name,
                                  @NonNull File canonicalTargetRoot) throws IOException {
        File out = new File(parentDir, name).getCanonicalFile();
        if (!isWithinRoot(out, canonicalTargetRoot)) {
            throw new SecurityException("Blocked unsafe path in selected directory: " + name);
        }
        return out;
    }

    private boolean isWithinRoot(@NonNull File candidate,
                                 @NonNull File canonicalTargetRoot) {
        String candidatePath = candidate.getPath();
        String rootPath = canonicalTargetRoot.getPath();
        return candidatePath.equals(rootPath)
                || candidatePath.startsWith(rootPath + File.separator);
    }

    private void ensureSharedObjectPermissions(@NonNull File prefixDir) {
        walkFiles(prefixDir, file -> {
            String name = file.getName();
            if (name.endsWith(".so") || name.contains(".so.")) {
                // Some extracted files may lose executable bit; keep runtime loadable.
                file.setReadable(true, false);
                file.setExecutable(true, false);
            }
        });
    }

    private interface FileConsumer {
        void accept(File file);
    }

    private void walkFiles(@NonNull File root, @NonNull FileConsumer consumer) {
        if (!root.exists()) {
            return;
        }
        if (root.isFile()) {
            consumer.accept(root);
            return;
        }
        File[] children = root.listFiles();
        if (children == null) {
            return;
        }
        for (File child : children) {
            if (child.isDirectory()) {
                walkFiles(child, consumer);
            } else {
                consumer.accept(child);
            }
        }
    }

    private void deleteRecursively(@NonNull File root) {
        if (!root.exists()) {
            return;
        }
        if (root.isDirectory()) {
            File[] children = root.listFiles();
            if (children != null) {
                for (File child : children) {
                    deleteRecursively(child);
                }
            }
        }
        // Best effort cleanup; non-critical leftovers are overwritten by next install.
        //noinspection ResultOfMethodCallIgnored
        root.delete();
    }

    private static void copy(@NonNull InputStream in, @NonNull FileOutputStream out) throws IOException {
        byte[] buffer = new byte[16 * 1024];
        int n;
        while ((n = in.read(buffer)) != -1) {
            out.write(buffer, 0, n);
        }
    }
}
