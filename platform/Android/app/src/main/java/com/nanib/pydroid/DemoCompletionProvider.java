package com.nanib.pydroid;

import android.util.Log;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.nanib.pydroid.python.PythonRuntime;
import com.qiplat.sweeteditor.SweetEditor;
import com.qiplat.sweeteditor.completion.CompletionContext;
import com.qiplat.sweeteditor.completion.CompletionItem;
import com.qiplat.sweeteditor.completion.CompletionProvider;
import com.qiplat.sweeteditor.completion.CompletionReceiver;
import com.qiplat.sweeteditor.completion.CompletionResult;
import com.qiplat.sweeteditor.core.Document;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.concurrent.Executors;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class DemoCompletionProvider implements CompletionProvider {

    private static final String TAG = "DemoCompletionProvider";

    private static final int MAX_RESULT_ITEMS = 720;
    private static final int MAX_CONTEXT_SYMBOLS = 900;
    private static final int MAX_MODULE_MEMBER_SCAN = 2200;
    private static final int MAX_CLASS_MEMBER_SCAN = 1200;
    private static final int MAX_STAR_IMPORT_DEPTH = 2;
    private static final int MAX_PACKAGE_SURFACE_SCAN = 240;

    private static final Set<String> TRIGGER_CHARS = new HashSet<>(Arrays.asList(".", "_"));

    private static final Set<String> PYTHON_KEYWORDS = new LinkedHashSet<>(Arrays.asList(
            "False", "None", "True", "and", "as", "assert", "async", "await", "break",
            "class", "continue", "def", "del", "elif", "else", "except", "finally", "for",
            "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not", "or",
            "pass", "raise", "return", "try", "while", "with", "yield"
    ));

    private static final Set<String> PYTHON_BUILTINS = new LinkedHashSet<>(Arrays.asList(
            "abs", "all", "any", "bool", "breakpoint", "bytes", "callable", "chr", "dict", "dir",
            "enumerate", "filter", "float", "format", "getattr", "hasattr", "hash", "help", "hex",
            "id", "input", "int", "isinstance", "issubclass", "iter", "len", "list", "locals",
            "map", "max", "min", "next", "object", "open", "ord", "pow", "print", "property",
            "range", "repr", "reversed", "round", "set", "slice", "sorted", "str", "sum", "super",
            "tuple", "type", "vars", "zip"
    ));

    private static final Set<String> BUILTIN_MODULES = new LinkedHashSet<>(Arrays.asList(
            "abc", "argparse", "asyncio", "base64", "collections", "concurrent", "contextlib",
            "copy", "csv", "ctypes", "datetime", "decimal", "enum", "functools", "glob", "hashlib",
            "heapq", "importlib", "inspect", "io", "itertools", "json", "logging", "math", "operator",
            "os", "pathlib", "pickle", "queue", "random", "re", "shutil", "signal", "socket",
            "sqlite3", "statistics", "string", "subprocess", "sys", "tempfile", "threading", "time",
            "traceback", "typing", "unittest", "urllib", "uuid", "warnings", "weakref", "xml", "zipfile"
    ));

    private static final Pattern TRAILING_IDENTIFIER_PATTERN = Pattern.compile("([A-Za-z_][A-Za-z0-9_]*)$");
    private static final Pattern ATTRIBUTE_ACCESS_PATTERN = Pattern.compile(
            "([A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*)\\.([A-Za-z0-9_]*)$"
    );

    private static final Pattern IMPORT_LINE_PATTERN = Pattern.compile("^\\s*import\\s+([A-Za-z0-9_\\.]*)$");
    private static final Pattern FROM_IMPORT_LINE_PATTERN = Pattern.compile(
            "^\\s*from\\s+([\\.A-Za-z0-9_]+)\\s+import\\s+([A-Za-z0-9_,\\s]*)$"
    );
    private static final Pattern FROM_MODULE_LINE_PATTERN = Pattern.compile("^\\s*from\\s+([A-Za-z0-9_\\.]*)$");

    private static final Pattern CONTEXT_IDENTIFIER_PATTERN = Pattern.compile("\\b([A-Za-z_][A-Za-z0-9_]*)\\b");
    private static final Pattern SELF_ATTRIBUTE_PATTERN = Pattern.compile("\\bself\\.([A-Za-z_][A-Za-z0-9_]*)");

    private static final Pattern TOP_DEF_PATTERN = Pattern.compile("^def\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(");
    private static final Pattern TOP_CLASS_PATTERN = Pattern.compile("^class\\s+([A-Za-z_][A-Za-z0-9_]*)\\b");
    private static final Pattern TOP_ASSIGN_PATTERN = Pattern.compile("^([A-Za-z_][A-Za-z0-9_]*)\\s*=");
    private static final Pattern TOP_FROM_IMPORT_PATTERN = Pattern.compile("^from\\s+[\\.A-Za-z0-9_]+\\s+import\\s+(.+)$");
    private static final Pattern STAR_FROM_IMPORT_PATTERN = Pattern.compile("^from\\s+([\\.A-Za-z0-9_]+)\\s+import\\s+\\*$");

    private static final Pattern CLASS_DEF_PATTERN = Pattern.compile("^class\\s+([A-Za-z_][A-Za-z0-9_]*)\\b");
    private static final Pattern CLASS_METHOD_PATTERN = Pattern.compile("^def\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(");
    private static final Pattern CLASS_ASSIGN_PATTERN = Pattern.compile("^([A-Za-z_][A-Za-z0-9_]*)\\s*=");

    private static final Pattern AS_ALIAS_SPLIT_PATTERN = Pattern.compile("\\s+as\\s+");

    private static final Map<String, List<MemberEntry>> DEFAULT_MODULE_MEMBERS = buildDefaultModuleMembers();
    private static final Map<String, List<MemberEntry>> DEFAULT_TYPE_MEMBERS = buildDefaultTypeMembers();
    private static final long MAX_COMPLETION_LOG_BYTES = 1L * 1024L * 1024L;

    private final SweetEditor editor;
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final ModuleCatalog moduleCatalog = new ModuleCatalog();
    private final Object completionLogLock = new Object();
    @NonNull
    private final File completionLogFile;

    @Nullable
    private volatile PythonRuntime.PythonEnvironment pythonEnvironment;

    public DemoCompletionProvider(@NonNull SweetEditor editor) {
        this.editor = editor;
        completionLogFile = new File(editor.getContext().getFilesDir(), "logs/completion_analysis.log");
        logCompletionEvent("Completion provider initialized");
    }

    public void setPythonEnvironment(@Nullable PythonRuntime.PythonEnvironment environment) {
        pythonEnvironment = environment;
        moduleCatalog.setEnvironment(environment);
    }

    public void invalidatePythonIndexes() {
        moduleCatalog.invalidate();
    }

    public void shutdown() {
        executor.shutdownNow();
    }

    private void logCompletionEvent(@NonNull String message) {
        synchronized (completionLogLock) {
            File parent = completionLogFile.getParentFile();
            if (parent != null && !parent.exists()) {
                //noinspection ResultOfMethodCallIgnored
                parent.mkdirs();
            }
            if (completionLogFile.exists() && completionLogFile.length() > MAX_COMPLETION_LOG_BYTES) {
                //noinspection ResultOfMethodCallIgnored
                completionLogFile.delete();
            }

            String timestamp = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss.SSS", Locale.US)
                    .format(new Date());
            try (OutputStreamWriter writer = new OutputStreamWriter(
                    new FileOutputStream(completionLogFile, true), StandardCharsets.UTF_8)) {
                writer.write(timestamp + " " + message + "\n");
            } catch (IOException ignored) {
            }
        }
    }

    @Override
    public boolean isTriggerCharacter(@NonNull String ch) {
        return TRIGGER_CHARS.contains(ch);
    }

    @Override
    public void provideCompletions(@NonNull CompletionContext context, @NonNull CompletionReceiver receiver) {
        if (isPythonContext(context)) {
            providePythonCompletions(context, receiver);
            return;
        }
        provideFallbackCompletions(context, receiver);
    }

    private void providePythonCompletions(@NonNull CompletionContext context,
                                          @NonNull CompletionReceiver receiver) {
        final Document document = editor.getDocument();
        final String documentText = document != null ? safeText(document.getText()) : "";
        final String linePrefix = safeLinePrefix(context.lineText, context.cursorPosition.column);
        final PythonRequest request = PythonRequest.parse(linePrefix);
        final ImportIndex importIndex = ImportIndex.parse(documentText);
        final PythonRuntime.PythonEnvironment env = pythonEnvironment;

        executor.submit(() -> {
            if (receiver.isCancelled()) {
                return;
            }

            LinkedHashMap<String, CompletionItem> resultMap = new LinkedHashMap<>();

            if (request.fromImportContext) {
                addModuleMembers(resultMap, request.fromModule, request.prefix, importIndex, env);
                addContextSymbols(resultMap, documentText, request.prefix);
            } else if (request.importModuleContext || request.fromModuleContext) {
                if (!request.moduleBase.isEmpty()) {
                    addSubmoduleCandidates(resultMap, request.moduleBase, request.prefix, env);
                } else {
                    addModuleCandidates(resultMap, request.prefix, env);
                }
            } else if (request.attributeContext) {
                addAttributeCandidates(resultMap, request, importIndex, documentText, env);
            } else {
                addKeywordCandidates(resultMap, request.prefix);
                addBuiltinFunctionCandidates(resultMap, request.prefix);
                addImportedAliasCandidates(resultMap, importIndex, request.prefix);
                addContextSymbols(resultMap, documentText, request.prefix);
                addModuleCandidates(resultMap, request.prefix, env);
            }

            List<CompletionItem> items = new ArrayList<>(resultMap.values());
            if (items.size() > MAX_RESULT_ITEMS) {
                items = new ArrayList<>(items.subList(0, MAX_RESULT_ITEMS));
            }
            receiver.accept(new CompletionResult(items, false));

            String completionLog = "Python completion: mode=" + request.mode()
                    + " qualifier='" + request.qualifier + "'"
                    + " prefix='" + request.prefix + "'"
                    + " env=" + (env != null ? "ready" : "null")
                    + " count=" + items.size();
            Log.i(TAG, completionLog);
            logCompletionEvent(completionLog);
        });
    }

    private void provideFallbackCompletions(@NonNull CompletionContext context,
                                            @NonNull CompletionReceiver receiver) {
        String prefix = extractTrailingIdentifier(safeLinePrefix(context.lineText, context.cursorPosition.column));

        executor.submit(() -> {
            if (receiver.isCancelled()) {
                return;
            }

            LinkedHashMap<String, CompletionItem> result = new LinkedHashMap<>();
            addSimpleCandidate(result, "if", "keyword", CompletionItem.KIND_KEYWORD, prefix, "fallback");
            addSimpleCandidate(result, "for", "keyword", CompletionItem.KIND_KEYWORD, prefix, "fallback");
            addSimpleCandidate(result, "while", "keyword", CompletionItem.KIND_KEYWORD, prefix, "fallback");
            addSimpleCandidate(result, "return", "keyword", CompletionItem.KIND_KEYWORD, prefix, "fallback");
            addSimpleCandidate(result, "class", "keyword", CompletionItem.KIND_KEYWORD, prefix, "fallback");
            receiver.accept(new CompletionResult(new ArrayList<>(result.values()), false));
        });
    }

    private static boolean isPythonContext(@NonNull CompletionContext context) {
        if (!(context.editorMetadata instanceof DemoFileMetadata)) {
            return false;
        }
        String fileName = ((DemoFileMetadata) context.editorMetadata).fileName;
        String lower = fileName.toLowerCase(Locale.ROOT);
        return lower.endsWith(".py") || lower.endsWith(".pyw") || lower.endsWith(".pyi");
    }

    private void addKeywordCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                      @NonNull String prefix) {
        for (String keyword : PYTHON_KEYWORDS) {
            addSimpleCandidate(result, keyword, "keyword", CompletionItem.KIND_KEYWORD, prefix, "kw");
        }
    }

    private void addBuiltinFunctionCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                              @NonNull String prefix) {
        for (String builtin : PYTHON_BUILTINS) {
            addSimpleCandidate(result, builtin, "builtin", CompletionItem.KIND_FUNCTION, prefix, "builtin");
        }
    }

    private void addImportedAliasCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                            @NonNull ImportIndex importIndex,
                                            @NonNull String prefix) {
        for (Map.Entry<String, String> entry : importIndex.moduleAliases.entrySet()) {
            String alias = entry.getKey();
            String module = entry.getValue();
            addSimpleCandidate(result,
                    alias,
                    "imported module: " + module,
                    CompletionItem.KIND_VARIABLE,
                    prefix,
                    "alias_mod");
        }

        for (Map.Entry<String, ImportedSymbol> entry : importIndex.importedSymbols.entrySet()) {
            String alias = entry.getKey();
            ImportedSymbol symbol = entry.getValue();
            addSimpleCandidate(result,
                    alias,
                    "from " + symbol.module + " import " + symbol.symbol,
                    CompletionItem.KIND_VARIABLE,
                    prefix,
                    "alias_sym");
        }
    }

    private void addContextSymbols(@NonNull LinkedHashMap<String, CompletionItem> result,
                                   @NonNull String documentText,
                                   @NonNull String prefix) {
        LinkedHashSet<String> symbols = extractContextIdentifiers(documentText);
        int count = 0;
        for (String symbol : symbols) {
            if (count >= MAX_CONTEXT_SYMBOLS) {
                break;
            }
            addSimpleCandidate(result,
                    symbol,
                    "context symbol",
                    CompletionItem.KIND_VARIABLE,
                    prefix,
                    "ctx");
            count++;
        }
    }

    private void addModuleCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                     @NonNull String prefix,
                                     @Nullable PythonRuntime.PythonEnvironment environment) {
        List<ModuleEntry> modules = moduleCatalog.getTopLevelModules(environment);
        for (ModuleEntry module : modules) {
            addSimpleCandidate(result,
                    module.name,
                    module.detail,
                    CompletionItem.KIND_MODULE,
                    prefix,
                    module.thirdParty ? "mod_third" : "mod_std");
        }
    }

    private void addSubmoduleCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                        @NonNull String baseModule,
                                        @NonNull String prefix,
                                        @Nullable PythonRuntime.PythonEnvironment environment) {
        List<String> children = moduleCatalog.getSubModules(baseModule, environment);
        for (String name : children) {
            addSimpleCandidate(result,
                    name,
                    "submodule of " + baseModule,
                    CompletionItem.KIND_MODULE,
                    prefix,
                    "submod");
        }
    }

    private void addAttributeCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                        @NonNull PythonRequest request,
                                        @NonNull ImportIndex importIndex,
                                        @NonNull String documentText,
                                        @Nullable PythonRuntime.PythonEnvironment environment) {
        String qualifier = request.qualifier;
        if (qualifier.isEmpty()) {
            return;
        }

        if ("self".equals(qualifier)) {
            addSelfAttributes(result, documentText, request.prefix);
        }

        boolean chainResolved = addChainedAttributeCandidates(
                result,
                qualifier,
                request.prefix,
                importIndex,
                environment
        );

        ImportedSymbol importedSymbol = importIndex.importedSymbols.get(qualifier);
        if (importedSymbol != null) {
            addImportedSymbolMembers(result, importedSymbol, request.prefix, environment);
        }

        String resolvedModule = resolveImportedModule(qualifier, importIndex, environment);
        if (resolvedModule != null && !resolvedModule.isEmpty()) {
            addModuleMembersDirect(result, resolvedModule, request.prefix, environment);
        }

        if (!chainResolved && qualifier.contains(".")) {
            int firstDot = qualifier.indexOf('.');
            if (firstDot > 0) {
                String head = qualifier.substring(0, firstDot);
                ImportedSymbol headSymbol = importIndex.importedSymbols.get(head);
                if (headSymbol != null) {
                    String candidateClass = headSymbol.module + "." + headSymbol.symbol;
                    String tail = qualifier.substring(firstDot + 1);
                    String moduleCandidate = candidateClass + "." + tail;
                    if (moduleCatalog.hasModule(moduleCandidate, environment)) {
                        addModuleMembersDirect(result, moduleCandidate, request.prefix, environment);
                    }
                }
            }
        }
    }

    private boolean addChainedAttributeCandidates(@NonNull LinkedHashMap<String, CompletionItem> result,
                                                  @NonNull String qualifier,
                                                  @NonNull String prefix,
                                                  @NonNull ImportIndex importIndex,
                                                  @Nullable PythonRuntime.PythonEnvironment environment) {
        String[] segments = qualifier.split("\\.");
        if (segments.length < 2) {
            return false;
        }

        String currentModule = resolveImportedModule(segments[0], importIndex, environment);
        if (currentModule == null || currentModule.isEmpty()) {
            return false;
        }

        List<MemberEntry> currentMembers = moduleCatalog.getModuleMembers(currentModule, environment);
        if (currentMembers.isEmpty()) {
            return false;
        }

        boolean progressed = false;
        for (int i = 1; i < segments.length; i++) {
            String segment = segments[i];
            if (segment.isEmpty()) {
                return false;
            }

            MemberEntry member = findMemberByName(currentMembers, segment);
            if (member == null) {
                return false;
            }
            progressed = true;

            boolean isLast = i == segments.length - 1;
            String moduleCandidate = currentModule + "." + segment;

            if (isLast) {
                if (moduleCatalog.hasModule(moduleCandidate, environment)) {
                    addModuleMembersDirect(result, moduleCandidate, prefix, environment);
                }
                List<MemberEntry> classMembers = moduleCatalog.getClassMembers(currentModule, segment, environment);
                addMemberEntries(result, classMembers, prefix, "chain_class_" + moduleCandidate);
                addTypeMembersForEntry(result, member, prefix);
                return true;
            }

            if (moduleCatalog.hasModule(moduleCandidate, environment)) {
                currentModule = moduleCandidate;
                currentMembers = moduleCatalog.getModuleMembers(currentModule, environment);
                continue;
            }

            List<MemberEntry> classMembers = moduleCatalog.getClassMembers(currentModule, segment, environment);
            if (!classMembers.isEmpty()) {
                currentModule = moduleCandidate;
                currentMembers = classMembers;
                continue;
            }

            String typeName = normalizeMemberType(member.detail);
            List<MemberEntry> typeMembers = DEFAULT_TYPE_MEMBERS.get(typeName);
            if (typeMembers != null && !typeMembers.isEmpty()) {
                currentModule = typeName;
                currentMembers = typeMembers;
                continue;
            }
            return false;
        }

        return progressed;
    }

    @Nullable
    private static MemberEntry findMemberByName(@NonNull List<MemberEntry> members,
                                                 @NonNull String name) {
        for (MemberEntry member : members) {
            if (name.equals(member.name)) {
                return member;
            }
        }
        return null;
    }

    private void addMemberEntries(@NonNull LinkedHashMap<String, CompletionItem> result,
                                  @NonNull List<MemberEntry> members,
                                  @NonNull String prefix,
                                  @NonNull String group) {
        for (MemberEntry member : members) {
            addSimpleCandidate(result, member.name, member.detail, member.kind, prefix, group);
        }
    }

    private void addTypeMembersForEntry(@NonNull LinkedHashMap<String, CompletionItem> result,
                                        @NonNull MemberEntry member,
                                        @NonNull String prefix) {
        String typeName = normalizeMemberType(member.detail);
        List<MemberEntry> typeMembers = DEFAULT_TYPE_MEMBERS.get(typeName);
        if (typeMembers == null || typeMembers.isEmpty()) {
            return;
        }
        addMemberEntries(result, typeMembers, prefix, "type_" + typeName);
    }

    @NonNull
    private static String normalizeMemberType(@NonNull String detail) {
        if (detail.isEmpty()) {
            return "";
        }
        String lower = detail.toLowerCase(Locale.ROOT).trim();
        int bracket = lower.indexOf('[');
        if (bracket > 0) {
            lower = lower.substring(0, bracket);
        }
        if (lower.startsWith("typing.")) {
            lower = lower.substring("typing.".length());
        }
        if ("mapping".equals(lower)) {
            lower = "dict";
        } else if ("sequence".equals(lower)) {
            lower = "list";
        } else if ("integer".equals(lower)) {
            lower = "int";
        }
        if (DEFAULT_TYPE_MEMBERS.containsKey(lower)) {
            return lower;
        }
        return "";
    }

    private void addImportedSymbolMembers(@NonNull LinkedHashMap<String, CompletionItem> result,
                                          @NonNull ImportedSymbol importedSymbol,
                                          @NonNull String prefix,
                                          @Nullable PythonRuntime.PythonEnvironment environment) {
        String moduleCandidate = importedSymbol.module + "." + importedSymbol.symbol;
        if (moduleCatalog.hasModule(moduleCandidate, environment)) {
            addModuleMembersDirect(result, moduleCandidate, prefix, environment);
            return;
        }

        List<MemberEntry> classMembers = moduleCatalog.getClassMembers(
                importedSymbol.module,
                importedSymbol.symbol,
                environment
        );
        if (!classMembers.isEmpty()) {
            addMemberEntries(result, classMembers, prefix, "class_member");
            return;
        }

        addModuleMembersDirect(result, importedSymbol.module, prefix, environment);
    }

    private void addSelfAttributes(@NonNull LinkedHashMap<String, CompletionItem> result,
                                   @NonNull String documentText,
                                   @NonNull String prefix) {
        Matcher matcher = SELF_ATTRIBUTE_PATTERN.matcher(documentText);
        LinkedHashSet<String> attrs = new LinkedHashSet<>();
        while (matcher.find()) {
            attrs.add(matcher.group(1));
            if (attrs.size() >= 300) {
                break;
            }
        }
        for (String attr : attrs) {
            addSimpleCandidate(result,
                    attr,
                    "instance attribute",
                    CompletionItem.KIND_PROPERTY,
                    prefix,
                    "self");
        }
    }

    private void addModuleMembers(@NonNull LinkedHashMap<String, CompletionItem> result,
                                  @NonNull String moduleOrAlias,
                                  @NonNull String prefix,
                                  @NonNull ImportIndex importIndex,
                                  @Nullable PythonRuntime.PythonEnvironment environment) {
        String resolvedModule = resolveImportedModule(moduleOrAlias, importIndex, environment);
        if (resolvedModule == null || resolvedModule.isEmpty()) {
            return;
        }
        addModuleMembersDirect(result, resolvedModule, prefix, environment);
    }

    private void addModuleMembersDirect(@NonNull LinkedHashMap<String, CompletionItem> result,
                                        @NonNull String moduleName,
                                        @NonNull String prefix,
                                        @Nullable PythonRuntime.PythonEnvironment environment) {
        List<MemberEntry> members = moduleCatalog.getModuleMembers(moduleName, environment);
        for (MemberEntry member : members) {
            addSimpleCandidate(result,
                    member.name,
                    member.detail,
                    member.kind,
                    prefix,
                    "member_" + moduleName);
        }
    }

    @Nullable
    private String resolveImportedModule(@NonNull String expression,
                                         @NonNull ImportIndex importIndex,
                                         @Nullable PythonRuntime.PythonEnvironment environment) {
        String directAlias = importIndex.moduleAliases.get(expression);
        if (directAlias != null) {
            return directAlias;
        }

        ImportedSymbol directImportedSymbol = importIndex.importedSymbols.get(expression);
        if (directImportedSymbol != null) {
            String candidate = directImportedSymbol.module + "." + directImportedSymbol.symbol;
            if (moduleCatalog.hasModule(candidate, environment)) {
                return candidate;
            }
            return directImportedSymbol.module;
        }

        int dot = expression.indexOf('.');
        if (dot > 0) {
            String head = expression.substring(0, dot);
            String tail = expression.substring(dot + 1);

            String headAlias = importIndex.moduleAliases.get(head);
            if (headAlias != null) {
                return headAlias + "." + tail;
            }

            ImportedSymbol headImported = importIndex.importedSymbols.get(head);
            if (headImported != null) {
                String moduleCandidate = headImported.module + "." + headImported.symbol + "." + tail;
                if (moduleCatalog.hasModule(moduleCandidate, environment)) {
                    return moduleCandidate;
                }

                String fallback = headImported.module + "." + tail;
                if (moduleCatalog.hasModule(fallback, environment)) {
                    return fallback;
                }
            }
        }

        if (moduleCatalog.hasModule(expression, environment)) {
            return expression;
        }

        return expression;
    }

    private static void addSimpleCandidate(@NonNull LinkedHashMap<String, CompletionItem> result,
                                           @NonNull String label,
                                           @NonNull String detail,
                                           int kind,
                                           @NonNull String prefix,
                                           @NonNull String group) {
        if (label.isEmpty()) {
            return;
        }
        if (!matchesPrefix(label, prefix)) {
            return;
        }
        String key = label + "|" + kind;
        if (result.containsKey(key)) {
            return;
        }

        CompletionItem item = new CompletionItem();
        item.label = label;
        item.insertText = label;
        item.detail = detail;
        item.kind = kind;
        item.filterText = label;
        item.sortKey = buildSortKey(label, prefix, group);

        result.put(key, item);
    }

    private static boolean matchesPrefix(@NonNull String value, @NonNull String prefix) {
        if (prefix.isEmpty()) {
            return true;
        }

        String lowerValue = value.toLowerCase(Locale.ROOT);
        String lowerPrefix = prefix.toLowerCase(Locale.ROOT);
        if (lowerValue.startsWith(lowerPrefix)) {
            return true;
        }

        // Fuzzy fallback: allow subsequence match for longer typed prefixes.
        return lowerPrefix.length() >= 2 && isSubsequence(lowerPrefix, lowerValue);
    }

    private static boolean isSubsequence(@NonNull String needle,
                                         @NonNull String haystack) {
        int i = 0;
        int j = 0;
        while (i < needle.length() && j < haystack.length()) {
            if (needle.charAt(i) == haystack.charAt(j)) {
                i++;
            }
            j++;
        }
        return i == needle.length();
    }

    @NonNull
    private static String buildSortKey(@NonNull String label,
                                       @NonNull String prefix,
                                       @NonNull String group) {
        String lowerLabel = label.toLowerCase(Locale.ROOT);
        String lowerPrefix = prefix.toLowerCase(Locale.ROOT);

        int matchRank = 3;
        if (lowerPrefix.isEmpty()) {
            matchRank = 1;
        } else if (lowerLabel.equals(lowerPrefix)) {
            matchRank = 0;
        } else if (lowerLabel.startsWith(lowerPrefix)) {
            matchRank = 1;
        } else if (isSubsequence(lowerPrefix, lowerLabel)) {
            matchRank = 2;
        }

        int visibilityRank = lowerLabel.startsWith("_") ? 2 : 0;
        int totalRank = matchRank + visibilityRank;
        return String.format(Locale.US, "%02d_%s_%s", totalRank, group, lowerLabel);
    }

    @NonNull
    private static LinkedHashSet<String> extractContextIdentifiers(@NonNull String text) {
        LinkedHashSet<String> output = new LinkedHashSet<>();
        Matcher matcher = CONTEXT_IDENTIFIER_PATTERN.matcher(text);
        while (matcher.find()) {
            String token = matcher.group(1);
            if (token == null || token.isEmpty()) {
                continue;
            }
            if (PYTHON_KEYWORDS.contains(token)) {
                continue;
            }
            output.add(token);
            if (output.size() >= MAX_CONTEXT_SYMBOLS) {
                break;
            }
        }
        return output;
    }

    @NonNull
    private static String safeLinePrefix(@Nullable String lineText, int column) {
        if (lineText == null || lineText.isEmpty()) {
            return "";
        }
        int end = Math.max(0, Math.min(column, lineText.length()));
        return lineText.substring(0, end);
    }

    @NonNull
    private static String safeText(@Nullable String text) {
        return text == null ? "" : text;
    }

    @NonNull
    private static String extractTrailingIdentifier(@NonNull String text) {
        Matcher matcher = TRAILING_IDENTIFIER_PATTERN.matcher(text);
        if (matcher.find()) {
            return matcher.group(1);
        }
        return "";
    }

    private static final class PythonRequest {
        @NonNull
        final String prefix;
        final boolean attributeContext;
        @NonNull
        final String qualifier;

        final boolean importModuleContext;
        final boolean fromImportContext;
        final boolean fromModuleContext;

        @NonNull
        final String fromModule;
        @NonNull
        final String moduleBase;

        private PythonRequest(@NonNull String prefix,
                              boolean attributeContext,
                              @NonNull String qualifier,
                              boolean importModuleContext,
                              boolean fromImportContext,
                              boolean fromModuleContext,
                              @NonNull String fromModule,
                              @NonNull String moduleBase) {
            this.prefix = prefix;
            this.attributeContext = attributeContext;
            this.qualifier = qualifier;
            this.importModuleContext = importModuleContext;
            this.fromImportContext = fromImportContext;
            this.fromModuleContext = fromModuleContext;
            this.fromModule = fromModule;
            this.moduleBase = moduleBase;
        }

        @NonNull
        static PythonRequest parse(@NonNull String linePrefix) {
            Matcher attrMatcher = ATTRIBUTE_ACCESS_PATTERN.matcher(linePrefix);
            if (attrMatcher.find()) {
                return new PythonRequest(
                        safeText(attrMatcher.group(2)),
                        true,
                        safeText(attrMatcher.group(1)),
                        false,
                        false,
                        false,
                        "",
                        ""
                );
            }

            Matcher fromImportMatcher = FROM_IMPORT_LINE_PATTERN.matcher(linePrefix);
            if (fromImportMatcher.find()) {
                String fromModule = safeText(fromImportMatcher.group(1));
                String importPart = safeText(fromImportMatcher.group(2));
                return new PythonRequest(
                        extractTrailingIdentifier(importPart),
                        false,
                        "",
                        false,
                        true,
                        false,
                        fromModule,
                        ""
                );
            }

            Matcher importMatcher = IMPORT_LINE_PATTERN.matcher(linePrefix);
            if (importMatcher.find()) {
                String importPart = safeText(importMatcher.group(1));
                int dot = importPart.lastIndexOf('.');
                if (dot > 0 && dot < importPart.length() - 1) {
                    return new PythonRequest(
                            importPart.substring(dot + 1),
                            false,
                            "",
                            true,
                            false,
                            false,
                            "",
                            importPart.substring(0, dot)
                    );
                }
                if (importPart.endsWith(".") && importPart.length() > 1) {
                    return new PythonRequest(
                            "",
                            false,
                            "",
                            true,
                            false,
                            false,
                            "",
                            importPart.substring(0, importPart.length() - 1)
                    );
                }
                return new PythonRequest(
                        extractTrailingIdentifier(importPart),
                        false,
                        "",
                        true,
                        false,
                        false,
                        "",
                        ""
                );
            }

            Matcher fromModuleMatcher = FROM_MODULE_LINE_PATTERN.matcher(linePrefix);
            if (fromModuleMatcher.find()) {
                String modulePart = safeText(fromModuleMatcher.group(1));
                int dot = modulePart.lastIndexOf('.');
                if (dot > 0 && dot < modulePart.length() - 1) {
                    return new PythonRequest(
                            modulePart.substring(dot + 1),
                            false,
                            "",
                            false,
                            false,
                            true,
                            "",
                            modulePart.substring(0, dot)
                    );
                }
                if (modulePart.endsWith(".") && modulePart.length() > 1) {
                    return new PythonRequest(
                            "",
                            false,
                            "",
                            false,
                            false,
                            true,
                            "",
                            modulePart.substring(0, modulePart.length() - 1)
                    );
                }
                return new PythonRequest(
                        extractTrailingIdentifier(modulePart),
                        false,
                        "",
                        false,
                        false,
                        true,
                        "",
                        ""
                );
            }

            return new PythonRequest(
                    extractTrailingIdentifier(linePrefix),
                    false,
                    "",
                    false,
                    false,
                    false,
                    "",
                    ""
            );
        }

        @NonNull
        String mode() {
            if (fromImportContext) {
                return "from-import";
            }
            if (importModuleContext) {
                return "import";
            }
            if (fromModuleContext) {
                return "from-module";
            }
            if (attributeContext) {
                return "attr";
            }
            return "default";
        }
    }

    private static final class ImportedSymbol {
        @NonNull
        final String module;
        @NonNull
        final String symbol;

        ImportedSymbol(@NonNull String module, @NonNull String symbol) {
            this.module = module;
            this.symbol = symbol;
        }
    }

    private static final class ImportIndex {
        @NonNull
        final Map<String, String> moduleAliases = new LinkedHashMap<>();
        @NonNull
        final Map<String, ImportedSymbol> importedSymbols = new LinkedHashMap<>();

        @NonNull
        static ImportIndex parse(@NonNull String documentText) {
            ImportIndex index = new ImportIndex();
            String[] lines = documentText.split("\\n");
            for (String line : lines) {
                String normalized = stripComment(line).trim();
                if (normalized.isEmpty()) {
                    continue;
                }
                parseImportLine(index, normalized);
                parseFromImportLine(index, normalized);
            }
            return index;
        }

        private static void parseImportLine(@NonNull ImportIndex index, @NonNull String line) {
            if (!line.startsWith("import ")) {
                return;
            }
            String body = line.substring("import ".length()).trim();
            if (body.isEmpty()) {
                return;
            }
            String[] parts = body.split(",");
            for (String part : parts) {
                String clause = part.trim();
                if (clause.isEmpty()) {
                    continue;
                }
                String[] aliasSplit = AS_ALIAS_SPLIT_PATTERN.split(clause);
                String module = aliasSplit[0].trim();
                if (module.isEmpty()) {
                    continue;
                }
                String alias;
                if (aliasSplit.length > 1 && !aliasSplit[1].trim().isEmpty()) {
                    alias = aliasSplit[1].trim();
                } else {
                    int dot = module.indexOf('.');
                    alias = dot > 0 ? module.substring(0, dot) : module;
                }
                if (!alias.isEmpty()) {
                    index.moduleAliases.put(alias, module);
                }
            }
        }

        private static void parseFromImportLine(@NonNull ImportIndex index, @NonNull String line) {
            Matcher matcher = TOP_FROM_IMPORT_PATTERN.matcher(line);
            if (!matcher.find()) {
                return;
            }

            int fromIndex = line.indexOf("from ");
            int importIndex = line.indexOf(" import ");
            if (fromIndex != 0 || importIndex <= "from ".length()) {
                return;
            }

            String module = line.substring("from ".length(), importIndex).trim();
            String body = matcher.group(1).trim();
            if (module.isEmpty() || body.isEmpty()) {
                return;
            }

            String[] parts = body.split(",");
            for (String part : parts) {
                String clause = part.trim();
                if (clause.isEmpty() || "*".equals(clause)) {
                    continue;
                }
                String[] aliasSplit = AS_ALIAS_SPLIT_PATTERN.split(clause);
                String symbol = aliasSplit[0].trim();
                if (symbol.isEmpty()) {
                    continue;
                }
                String alias = aliasSplit.length > 1 && !aliasSplit[1].trim().isEmpty()
                        ? aliasSplit[1].trim()
                        : symbol;
                index.importedSymbols.put(alias, new ImportedSymbol(module, symbol));
            }
        }

        @NonNull
        private static String stripComment(@NonNull String raw) {
            int idx = raw.indexOf('#');
            if (idx >= 0) {
                return raw.substring(0, idx);
            }
            return raw;
        }
    }

    private static final class ModuleCatalog {
        private static final long REBUILD_INTERVAL_MS = 12_000L;

        private final Object lock = new Object();

        @Nullable
        private String environmentKey;
        private long lastBuildAt;

        @NonNull
        private List<ModuleEntry> topLevelModules = Collections.emptyList();

        @NonNull
        private final Map<String, List<MemberEntry>> moduleMembersCache = new HashMap<>();
        @NonNull
        private final Map<String, List<MemberEntry>> classMembersCache = new HashMap<>();
        @NonNull
        private final Map<String, List<String>> subModuleCache = new HashMap<>();
        @NonNull
        private final Map<String, Boolean> moduleExistsCache = new HashMap<>();

        void setEnvironment(@Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                String newKey = buildEnvironmentKey(environment);
                if (!equalsNullable(environmentKey, newKey)) {
                    environmentKey = newKey;
                    clearLocked();
                }
            }
        }

        void invalidate() {
            synchronized (lock) {
                clearLocked();
            }
        }

        boolean hasModule(@NonNull String moduleName,
                          @Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                ensureTopLevelModulesLocked(environment);

                String key = cacheKey("exists", moduleName);
                Boolean cached = moduleExistsCache.get(key);
                if (cached != null) {
                    return cached;
                }

                boolean exists = DEFAULT_MODULE_MEMBERS.containsKey(moduleName);

                if (!exists && environment != null) {
                    exists = resolveModuleSource(moduleName, environment) != null;
                }

                if (!exists) {
                    List<ModuleEntry> modules = topLevelModules;
                    for (ModuleEntry module : modules) {
                        if (module.name.equals(moduleName)) {
                            exists = true;
                            break;
                        }
                    }
                }

                moduleExistsCache.put(key, exists);
                return exists;
            }
        }

        @NonNull
        List<ModuleEntry> getTopLevelModules(@Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                ensureTopLevelModulesLocked(environment);
                return new ArrayList<>(topLevelModules);
            }
        }

        @NonNull
        List<String> getSubModules(@NonNull String moduleName,
                                   @Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                ensureTopLevelModulesLocked(environment);

                String key = cacheKey("sub", moduleName);
                List<String> cached = subModuleCache.get(key);
                if (cached != null) {
                    return cached;
                }

                LinkedHashSet<String> result = new LinkedHashSet<>();

                if ("os".equals(moduleName)) {
                    result.add("path");
                }
                if ("urllib".equals(moduleName)) {
                    result.add("parse");
                    result.add("request");
                    result.add("error");
                }
                if ("xml".equals(moduleName)) {
                    result.add("etree");
                    result.add("dom");
                }

                if (environment != null) {
                    File dir = resolveModuleDirectory(moduleName, environment);
                    if (dir != null && dir.isDirectory()) {
                        scanDirectoryForSubModules(dir, result);
                    }
                }

                List<String> sorted = new ArrayList<>(result);
                Collections.sort(sorted, String.CASE_INSENSITIVE_ORDER);
                subModuleCache.put(key, sorted);
                return sorted;
            }
        }

        @NonNull
        List<MemberEntry> getModuleMembers(@NonNull String moduleName,
                                           @Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                ensureTopLevelModulesLocked(environment);

                String key = cacheKey("module", moduleName);
                List<MemberEntry> cached = moduleMembersCache.get(key);
                if (cached != null) {
                    return cached;
                }

                LinkedHashMap<String, MemberEntry> result = new LinkedHashMap<>();
                addDefaultMembers(result, moduleName);

                if (environment != null) {
                    File source = resolveModuleSource(moduleName, environment);
                    if (source != null && source.isFile()) {
                        parseTopLevelMembersFromSource(source, moduleName, result);
                        parseStarImportedModules(source, moduleName, environment, result, new HashSet<>(), 0);
                    } else {
                        int dot = moduleName.lastIndexOf('.');
                        if (dot > 0 && dot < moduleName.length() - 1) {
                            String parentModule = moduleName.substring(0, dot);
                            String symbol = moduleName.substring(dot + 1);
                            File parentSource = resolveModuleSource(parentModule, environment);
                            if (parentSource != null && parentSource.isFile()) {
                                parseClassMembersFromSource(parentSource, symbol, result);
                            }
                        }
                    }
                }

                if (environment != null && result.size() < 64) {
                    augmentMembersFromPackageDirectory(moduleName, environment, result);
                }

                List<MemberEntry> members = new ArrayList<>(result.values());
                moduleMembersCache.put(key, members);
                return members;
            }
        }

        @NonNull
        List<MemberEntry> getClassMembers(@NonNull String moduleName,
                                          @NonNull String className,
                                          @Nullable PythonRuntime.PythonEnvironment environment) {
            synchronized (lock) {
                ensureTopLevelModulesLocked(environment);

                String key = cacheKey("class", moduleName + "." + className);
                List<MemberEntry> cached = classMembersCache.get(key);
                if (cached != null) {
                    return cached;
                }

                LinkedHashMap<String, MemberEntry> members = new LinkedHashMap<>();
                if (environment != null) {
                    File source = resolveModuleSource(moduleName, environment);
                    if (source != null && source.isFile()) {
                        parseClassMembersFromSource(source, className, members);
                    }
                }

                List<MemberEntry> output = new ArrayList<>(members.values());
                classMembersCache.put(key, output);
                return output;
            }
        }

        private void ensureTopLevelModulesLocked(@Nullable PythonRuntime.PythonEnvironment environment) {
            long now = System.currentTimeMillis();
            if (!topLevelModules.isEmpty() && (now - lastBuildAt) < REBUILD_INTERVAL_MS) {
                return;
            }

            LinkedHashMap<String, ModuleEntry> modules = new LinkedHashMap<>();
            for (String builtin : BUILTIN_MODULES) {
                modules.put(builtin, new ModuleEntry(builtin, "builtin module", false));
            }

            if (environment != null) {
                List<ModuleRoot> roots = collectModuleRoots(environment);
                for (ModuleRoot root : roots) {
                    scanRootForTopLevelModules(root, modules);
                }
            }

            List<ModuleEntry> sorted = new ArrayList<>(modules.values());
            Collections.sort(sorted, (a, b) -> a.name.compareToIgnoreCase(b.name));
            topLevelModules = sorted;
            lastBuildAt = now;
        }
        private void addDefaultMembers(@NonNull LinkedHashMap<String, MemberEntry> out,
                                       @NonNull String moduleName) {
            List<MemberEntry> direct = DEFAULT_MODULE_MEMBERS.get(moduleName);
            if (direct == null) {
                return;
            }
            for (MemberEntry entry : direct) {
                out.put(entry.name, entry);
            }
        }
        private void parseTopLevelMembersFromSource(@NonNull File source,
                                                    @NonNull String moduleName,
                                                    @NonNull LinkedHashMap<String, MemberEntry> out) {
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    new FileInputStream(source), StandardCharsets.UTF_8))) {
                String line;
                int scanned = 0;

                boolean inAllBlock = false;
                StringBuilder allBuffer = new StringBuilder();

                boolean inFromImportBlock = false;
                StringBuilder fromImportBuffer = new StringBuilder();

                while ((line = reader.readLine()) != null) {
                    scanned++;
                    if (scanned > MAX_MODULE_MEMBER_SCAN) {
                        break;
                    }

                    String stripped = stripComment(line);
                    String trimmed = stripped.trim();
                    if (trimmed.isEmpty()) {
                        continue;
                    }

                    // Preserve multiline __all__ blocks before indent filtering.
                    if (inAllBlock) {
                        allBuffer.append(" ").append(trimmed);
                        if (trimmed.contains("]") || trimmed.contains(")")) {
                            collectAllNames(allBuffer.toString(), out);
                            inAllBlock = false;
                        }
                        continue;
                    }

                    // Collect multiline "from x import (...)" blocks.
                    if (inFromImportBlock) {
                        fromImportBuffer.append(" ").append(trimmed);
                        if (trimmed.contains(")") || !trimmed.endsWith(",")) {
                            collectTopLevelFromImportNames(fromImportBuffer.toString(), out);
                            inFromImportBlock = false;
                        }
                        continue;
                    }

                    int indent = leadingSpaceCount(stripped);
                    if (indent != 0) {
                        continue;
                    }

                    if (trimmed.startsWith("__all__")) {
                        inAllBlock = true;
                        allBuffer.setLength(0);
                        allBuffer.append(trimmed);
                        if (trimmed.contains("]") || trimmed.contains(")")) {
                            collectAllNames(allBuffer.toString(), out);
                            inAllBlock = false;
                        }
                        continue;
                    }

                    Matcher defMatcher = TOP_DEF_PATTERN.matcher(trimmed);
                    if (defMatcher.find()) {
                        putMember(out, defMatcher.group(1), "function", CompletionItem.KIND_FUNCTION);
                    }

                    Matcher classMatcher = TOP_CLASS_PATTERN.matcher(trimmed);
                    if (classMatcher.find()) {
                        putMember(out, classMatcher.group(1), "class", CompletionItem.KIND_CLASS);
                    }

                    Matcher assignMatcher = TOP_ASSIGN_PATTERN.matcher(trimmed);
                    if (assignMatcher.find()) {
                        String name = assignMatcher.group(1);
                        if (!"__all__".equals(name)) {
                            putMember(out, name, "variable", CompletionItem.KIND_VARIABLE);
                        }
                    }

                    Matcher fromImportMatcher = TOP_FROM_IMPORT_PATTERN.matcher(trimmed);
                    if (fromImportMatcher.find()) {
                        String importPart = fromImportMatcher.group(1).trim();
                        if (importPart.endsWith("(") && !importPart.contains(")")) {
                            inFromImportBlock = true;
                            fromImportBuffer.setLength(0);
                            fromImportBuffer.append(trimmed);
                        } else {
                            collectTopLevelFromImportNames(trimmed, out);
                        }
                    }
                }

                String parseLog = "Module members parsed: module=" + moduleName
                        + " file=" + source.getName()
                        + " count=" + out.size();
                Log.i(TAG, parseLog);
            } catch (IOException ignored) {
            }
        }

        private void parseStarImportedModules(@NonNull File source,
                                              @NonNull String moduleName,
                                              @NonNull PythonRuntime.PythonEnvironment environment,
                                              @NonNull LinkedHashMap<String, MemberEntry> out,
                                              @NonNull Set<String> visitedModules,
                                              int depth) {
            if (depth > MAX_STAR_IMPORT_DEPTH) {
                return;
            }
            if (!visitedModules.add(moduleName)) {
                return;
            }

            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    new FileInputStream(source), StandardCharsets.UTF_8))) {
                String line;
                int scanned = 0;
                while ((line = reader.readLine()) != null) {
                    scanned++;
                    if (scanned > MAX_MODULE_MEMBER_SCAN) {
                        break;
                    }

                    String stripped = stripComment(line);
                    String trimmed = stripped.trim();
                    if (trimmed.isEmpty()) {
                        continue;
                    }

                    if (leadingSpaceCount(stripped) != 0) {
                        continue;
                    }

                    Matcher starImportMatcher = STAR_FROM_IMPORT_PATTERN.matcher(trimmed);
                    if (!starImportMatcher.find()) {
                        continue;
                    }

                    String targetModule = resolveRelativeImportModule(moduleName, source, starImportMatcher.group(1));
                    if (targetModule == null || targetModule.isEmpty()) {
                        continue;
                    }

                    File targetSource = resolveModuleSource(targetModule, environment);
                    if (targetSource == null || !targetSource.isFile()) {
                        continue;
                    }

                    parseTopLevelMembersFromSource(targetSource, targetModule, out);
                    parseStarImportedModules(targetSource, targetModule, environment, out, visitedModules, depth + 1);
                }
            } catch (IOException ignored) {
            }
        }

        @Nullable
        private static String resolveRelativeImportModule(@NonNull String moduleName,
                                                          @NonNull File source,
                                                          @NonNull String importTarget) {
            if (importTarget.isEmpty()) {
                return null;
            }

            int dotCount = 0;
            while (dotCount < importTarget.length() && importTarget.charAt(dotCount) == '.') {
                dotCount++;
            }

            if (dotCount == 0) {
                return importTarget;
            }

            String remainder = importTarget.substring(dotCount);
            String[] moduleParts = moduleName.split("\\.");
            if (moduleParts.length == 0) {
                return remainder.isEmpty() ? null : remainder;
            }

            int baseLength = moduleParts.length;
            boolean packageSource = source.getName().startsWith("__init__.");
            if (!packageSource && baseLength > 0) {
                baseLength -= 1;
            }

            int keep = baseLength - (dotCount - 1);
            if (keep <= 0) {
                return remainder.isEmpty() ? null : remainder;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < keep; i++) {
                if (i > 0) {
                    sb.append('.');
                }
                sb.append(moduleParts[i]);
            }
            if (!remainder.isEmpty()) {
                sb.append('.').append(remainder);
            }
            return sb.toString();
        }

        private static void collectTopLevelFromImportNames(@NonNull String raw,
                                                           @NonNull LinkedHashMap<String, MemberEntry> out) {
            Matcher fromImportMatcher = TOP_FROM_IMPORT_PATTERN.matcher(raw);
            if (!fromImportMatcher.find()) {
                return;
            }

            String importPart = fromImportMatcher.group(1)
                    .replace('(', ' ')
                    .replace(')', ' ');

            String[] imports = importPart.split(",");
            for (String part : imports) {
                String clause = part.trim();
                if (clause.isEmpty() || "*".equals(clause)) {
                    continue;
                }
                String[] aliasSplit = AS_ALIAS_SPLIT_PATTERN.split(clause);
                String name = aliasSplit[0].trim();
                if (!name.isEmpty()) {
                    putMember(out, name, "imported", CompletionItem.KIND_VARIABLE);
                }
            }
        }

        private void parseClassMembersFromSource(@NonNull File source,
                                                 @NonNull String className,
                                                 @NonNull LinkedHashMap<String, MemberEntry> out) {
            List<String> lines = readAllLines(source);
            if (lines.isEmpty()) {
                return;
            }

            int classLine = -1;
            int classIndent = -1;

            for (int i = 0; i < lines.size(); i++) {
                String raw = stripComment(lines.get(i));
                String trimmed = raw.trim();
                if (trimmed.isEmpty()) {
                    continue;
                }
                Matcher classMatcher = CLASS_DEF_PATTERN.matcher(trimmed);
                if (!classMatcher.find()) {
                    continue;
                }
                String found = classMatcher.group(1);
                if (!className.equals(found)) {
                    continue;
                }
                classLine = i;
                classIndent = leadingSpaceCount(raw);
                break;
            }

            if (classLine < 0 || classIndent < 0) {
                return;
            }

            int scanned = 0;
            for (int i = classLine + 1; i < lines.size(); i++) {
                if (scanned++ > MAX_CLASS_MEMBER_SCAN) {
                    break;
                }

                String raw = stripComment(lines.get(i));
                String trimmed = raw.trim();
                if (trimmed.isEmpty()) {
                    continue;
                }

                int indent = leadingSpaceCount(raw);
                if (indent <= classIndent) {
                    break;
                }

                Matcher methodMatcher = CLASS_METHOD_PATTERN.matcher(trimmed);
                if (methodMatcher.find()) {
                    String name = methodMatcher.group(1);
                    if (!"__init__".equals(name)) {
                        putMember(out, name, "method", CompletionItem.KIND_FUNCTION);
                    }
                }

                Matcher classAssignMatcher = CLASS_ASSIGN_PATTERN.matcher(trimmed);
                if (classAssignMatcher.find()) {
                    putMember(out, classAssignMatcher.group(1), "property", CompletionItem.KIND_PROPERTY);
                }

                Matcher selfMatcher = SELF_ATTRIBUTE_PATTERN.matcher(raw);
                while (selfMatcher.find()) {
                    putMember(out, selfMatcher.group(1), "property", CompletionItem.KIND_PROPERTY);
                }
            }
        }

        private static void collectAllNames(@NonNull String raw,
                                            @NonNull LinkedHashMap<String, MemberEntry> out) {
            Matcher quote = Pattern.compile("['\"]([A-Za-z_][A-Za-z0-9_]*)['\"]").matcher(raw);
            while (quote.find()) {
                putMember(out, quote.group(1), "exported", CompletionItem.KIND_VARIABLE);
            }
        }

        @NonNull
        private static List<String> readAllLines(@NonNull File source) {
            List<String> lines = new ArrayList<>();
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    new FileInputStream(source), StandardCharsets.UTF_8))) {
                String line;
                while ((line = reader.readLine()) != null) {
                    lines.add(line);
                }
            } catch (IOException ignored) {
            }
            return lines;
        }

        private void augmentMembersFromPackageDirectory(@NonNull String moduleName,
                                                        @NonNull PythonRuntime.PythonEnvironment environment,
                                                        @NonNull LinkedHashMap<String, MemberEntry> out) {
            File dir = resolveModuleDirectory(moduleName, environment);
            if (dir == null || !dir.isDirectory()) {
                return;
            }

            File[] entries = dir.listFiles();
            if (entries == null || entries.length == 0) {
                return;
            }

            int scanned = 0;
            for (File entry : entries) {
                if (scanned++ >= MAX_PACKAGE_SURFACE_SCAN) {
                    break;
                }
                if (out.size() >= (MAX_RESULT_ITEMS * 2)) {
                    break;
                }

                String name = entry.getName();
                if (name.isEmpty() || name.startsWith(".")) {
                    continue;
                }
                if ("__pycache__".equals(name)) {
                    continue;
                }

                if (entry.isFile()) {
                    String childModule = moduleNameFromFile(name);
                    if (childModule == null) {
                        continue;
                    }

                    putMember(out, childModule, "submodule", CompletionItem.KIND_MODULE);
                    if ((name.endsWith(".py") || name.endsWith(".pyi"))
                            && !"__init__.py".equals(name)
                            && !"__init__.pyi".equals(name)) {
                        parseTopLevelMembersFromSource(entry, moduleName + "." + childModule, out);
                    }
                    continue;
                }

                if (!entry.isDirectory() || !isValidIdentifier(name) || !isPackageDirectory(entry)) {
                    continue;
                }

                putMember(out, name, "submodule", CompletionItem.KIND_MODULE);
                File initPy = new File(entry, "__init__.py");
                File initPyi = new File(entry, "__init__.pyi");
                if (initPy.isFile()) {
                    parseTopLevelMembersFromSource(initPy, moduleName + "." + name, out);
                } else if (initPyi.isFile()) {
                    parseTopLevelMembersFromSource(initPyi, moduleName + "." + name, out);
                }
            }
        }

        @Nullable
        private File resolveModuleSource(@NonNull String moduleName,
                                         @NonNull PythonRuntime.PythonEnvironment environment) {
            String[] parts = moduleName.split("\\.");
            if (parts.length == 0) {
                return null;
            }

            List<ModuleRoot> roots = collectModuleRoots(environment);
            for (ModuleRoot root : roots) {
                File current = root.path;
                boolean failed = false;
                for (int i = 0; i < parts.length; i++) {
                    String part = parts[i];
                    boolean last = (i == parts.length - 1);

                    if (last) {
                        File pyFile = new File(current, part + ".py");
                        if (pyFile.isFile()) {
                            return pyFile;
                        }

                        File pyiFile = new File(current, part + ".pyi");
                        if (pyiFile.isFile()) {
                            return pyiFile;
                        }

                        File dir = new File(current, part);
                        File initPy = new File(dir, "__init__.py");
                        if (initPy.isFile()) {
                            return initPy;
                        }

                        File initPyi = new File(dir, "__init__.pyi");
                        if (initPyi.isFile()) {
                            return initPyi;
                        }
                    }

                    File next = new File(current, part);
                    if (!next.isDirectory()) {
                        failed = true;
                        break;
                    }
                    current = next;
                }
                if (!failed) {
                    // continue scanning other roots if needed
                }
            }
            return null;
        }

        @Nullable
        private File resolveModuleDirectory(@NonNull String moduleName,
                                            @NonNull PythonRuntime.PythonEnvironment environment) {
            String[] parts = moduleName.split("\\.");
            if (parts.length == 0) {
                return null;
            }

            List<ModuleRoot> roots = collectModuleRoots(environment);
            for (ModuleRoot root : roots) {
                File current = root.path;
                boolean failed = false;

                for (int i = 0; i < parts.length; i++) {
                    String part = parts[i];
                    boolean last = (i == parts.length - 1);
                    File next = new File(current, part);

                    if (!next.exists()) {
                        failed = true;
                        break;
                    }

                    if (last) {
                        if (next.isDirectory()) {
                            return next;
                        }
                        failed = true;
                        break;
                    }

                    if (!next.isDirectory()) {
                        failed = true;
                        break;
                    }
                    current = next;
                }

                if (!failed) {
                    // continue searching
                }
            }
            return null;
        }

        @NonNull
        private static List<ModuleRoot> collectModuleRoots(@NonNull PythonRuntime.PythonEnvironment environment) {
            List<ModuleRoot> roots = new ArrayList<>();

            File stdlib = environment.getStdlibDir();
            if (stdlib.isDirectory()) {
                roots.add(new ModuleRoot(stdlib, false));
            }

            File stdSite = new File(stdlib, "site-packages");
            if (stdSite.isDirectory()) {
                roots.add(new ModuleRoot(stdSite, true));
            }

            if (environment.userSiteDir.isDirectory()) {
                roots.add(new ModuleRoot(environment.userSiteDir, true));
            }

            return roots;
        }

        private static void scanRootForTopLevelModules(@NonNull ModuleRoot root,
                                                       @NonNull LinkedHashMap<String, ModuleEntry> out) {
            File[] entries = root.path.listFiles();
            if (entries == null || entries.length == 0) {
                return;
            }

            for (File entry : entries) {
                String name = entry.getName();
                if (name.isEmpty() || name.startsWith(".")) {
                    continue;
                }
                if ("__pycache__".equals(name)) {
                    continue;
                }

                if (entry.isFile()) {
                    String module = moduleNameFromFile(name);
                    if (module != null) {
                        out.put(module, new ModuleEntry(
                                module,
                                root.thirdParty ? "third-party module" : "stdlib module",
                                root.thirdParty
                        ));
                    }
                    continue;
                }

                if (entry.isDirectory() && isPackageDirectory(entry)) {
                    if (isValidIdentifier(name)) {
                        out.put(name, new ModuleEntry(
                                name,
                                root.thirdParty ? "third-party package" : "stdlib package",
                                root.thirdParty
                        ));
                    }
                }
            }
        }

        private static void scanDirectoryForSubModules(@NonNull File dir,
                                                       @NonNull LinkedHashSet<String> out) {
            File[] entries = dir.listFiles();
            if (entries == null || entries.length == 0) {
                return;
            }

            for (File entry : entries) {
                String name = entry.getName();
                if (name.isEmpty() || name.startsWith(".")) {
                    continue;
                }
                if ("__pycache__".equals(name)) {
                    continue;
                }

                if (entry.isFile()) {
                    String module = moduleNameFromFile(name);
                    if (module != null) {
                        out.add(module);
                    }
                    continue;
                }

                if (entry.isDirectory() && isPackageDirectory(entry) && isValidIdentifier(name)) {
                    out.add(name);
                }
            }
        }

        private static boolean isPackageDirectory(@NonNull File dir) {
            File init = new File(dir, "__init__.py");
            if (init.isFile()) {
                return true;
            }
            File initPyi = new File(dir, "__init__.pyi");
            if (initPyi.isFile()) {
                return true;
            }
            File[] files = dir.listFiles();
            if (files == null) {
                return false;
            }
            for (File file : files) {
                if (file.isFile() && (file.getName().endsWith(".py") || file.getName().endsWith(".pyi"))) {
                    return true;
                }
            }
            return false;
        }

        @Nullable
        private static String moduleNameFromFile(@NonNull String fileName) {
            if (fileName.endsWith(".py") && !fileName.equals("__init__.py")) {
                String base = fileName.substring(0, fileName.length() - 3);
                return isValidIdentifier(base) ? base : null;
            }
            if (fileName.endsWith(".pyi") && !fileName.equals("__init__.pyi")) {
                String base = fileName.substring(0, fileName.length() - 4);
                return isValidIdentifier(base) ? base : null;
            }
            if (fileName.endsWith(".so") || fileName.endsWith(".pyd")) {
                int dot = fileName.indexOf('.');
                if (dot > 0) {
                    String base = fileName.substring(0, dot);
                    return isValidIdentifier(base) ? base : null;
                }
            }
            return null;
        }

        private static boolean isValidIdentifier(@NonNull String value) {
            return value.matches("[A-Za-z_][A-Za-z0-9_]*");
        }

        @NonNull
        private static String stripComment(@NonNull String raw) {
            int idx = raw.indexOf('#');
            if (idx >= 0) {
                return raw.substring(0, idx);
            }
            return raw;
        }

        private static int leadingSpaceCount(@NonNull String line) {
            int count = 0;
            for (int i = 0; i < line.length(); i++) {
                char ch = line.charAt(i);
                if (ch == ' ') {
                    count++;
                    continue;
                }
                if (ch == '\t') {
                    count += 4;
                    continue;
                }
                break;
            }
            return count;
        }

        private static void putMember(@NonNull LinkedHashMap<String, MemberEntry> out,
                                      @Nullable String name,
                                      @NonNull String detail,
                                      int kind) {
            if (name == null || name.isEmpty()) {
                return;
            }
            if (name.startsWith("__") && name.endsWith("__")) {
                return;
            }
            if (!out.containsKey(name)) {
                out.put(name, new MemberEntry(name, detail, kind));
            }
        }

        private static boolean equalsNullable(@Nullable String a, @Nullable String b) {
            if (a == null) {
                return b == null;
            }
            return a.equals(b);
        }

        @Nullable
        private static String buildEnvironmentKey(@Nullable PythonRuntime.PythonEnvironment environment) {
            if (environment == null) {
                return null;
            }
            return environment.prefixDir.getAbsolutePath() + "|" + environment.userSiteDir.getAbsolutePath();
        }

        @NonNull
        private String cacheKey(@NonNull String kind, @NonNull String value) {
            return (environmentKey == null ? "noenv" : environmentKey) + "|" + kind + "|" + value;
        }

        private void clearLocked() {
            lastBuildAt = 0L;
            topLevelModules = Collections.emptyList();
            moduleMembersCache.clear();
            classMembersCache.clear();
            subModuleCache.clear();
            moduleExistsCache.clear();
        }

        @NonNull
        private static String rootModuleName(@NonNull String moduleName) {
            int dot = moduleName.indexOf('.');
            if (dot > 0) {
                return moduleName.substring(0, dot);
            }
            return moduleName;
        }
    }

    private static final class ModuleRoot {
        @NonNull
        final File path;
        final boolean thirdParty;

        ModuleRoot(@NonNull File path, boolean thirdParty) {
            this.path = path;
            this.thirdParty = thirdParty;
        }
    }

    private static final class ModuleEntry {
        @NonNull
        final String name;
        @NonNull
        final String detail;
        final boolean thirdParty;

        ModuleEntry(@NonNull String name, @NonNull String detail, boolean thirdParty) {
            this.name = name;
            this.detail = detail;
            this.thirdParty = thirdParty;
        }
    }

    private static final class MemberEntry {
        @NonNull
        final String name;
        @NonNull
        final String detail;
        final int kind;

        MemberEntry(@NonNull String name, @NonNull String detail, int kind) {
            this.name = name;
            this.detail = detail;
            this.kind = kind;
        }
    }


    @NonNull
    private static Map<String, List<MemberEntry>> buildDefaultTypeMembers() {
        Map<String, List<MemberEntry>> map = new HashMap<>();

        map.put("str", Arrays.asList(
                new MemberEntry("capitalize", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("casefold", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("count", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("encode", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("endswith", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("find", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("format", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("isalnum", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("isalpha", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("isdigit", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("join", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("lower", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("replace", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("split", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("startswith", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("strip", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("upper", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("list", Arrays.asList(
                new MemberEntry("append", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("clear", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("copy", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("count", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("extend", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("index", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("insert", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("pop", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("remove", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("reverse", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("sort", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("dict", Arrays.asList(
                new MemberEntry("clear", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("copy", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("get", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("items", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("keys", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("pop", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("setdefault", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("update", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("values", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("set", Arrays.asList(
                new MemberEntry("add", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("clear", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("difference", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("discard", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("intersection", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("pop", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("remove", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("union", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("tuple", Arrays.asList(
                new MemberEntry("count", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("index", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("bytes", Arrays.asList(
                new MemberEntry("decode", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("find", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("hex", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("replace", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("split", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("int", Arrays.asList(
                new MemberEntry("bit_length", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("to_bytes", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("float", Arrays.asList(
                new MemberEntry("as_integer_ratio", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("hex", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("is_integer", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("bool", Arrays.asList(
                new MemberEntry("bit_count", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("to_bytes", "function", CompletionItem.KIND_FUNCTION)
        ));

        return map;
    }
    @NonNull
    private static Map<String, List<MemberEntry>> buildDefaultModuleMembers() {
        Map<String, List<MemberEntry>> map = new HashMap<>();

        map.put("os", Arrays.asList(
                new MemberEntry("path", "module", CompletionItem.KIND_MODULE),
                new MemberEntry("listdir", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("getcwd", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("environ", "mapping", CompletionItem.KIND_VARIABLE),
                new MemberEntry("mkdir", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("remove", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("sys", Arrays.asList(
                new MemberEntry("argv", "list", CompletionItem.KIND_VARIABLE),
                new MemberEntry("path", "list", CompletionItem.KIND_VARIABLE),
                new MemberEntry("platform", "str", CompletionItem.KIND_VARIABLE),
                new MemberEntry("version", "str", CompletionItem.KIND_VARIABLE),
                new MemberEntry("version_info", "tuple", CompletionItem.KIND_VARIABLE),
                new MemberEntry("prefix", "str", CompletionItem.KIND_VARIABLE),
                new MemberEntry("base_prefix", "str", CompletionItem.KIND_VARIABLE),
                new MemberEntry("executable", "str", CompletionItem.KIND_VARIABLE),
                new MemberEntry("modules", "dict", CompletionItem.KIND_VARIABLE),
                new MemberEntry("exit", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("stdout", "stream", CompletionItem.KIND_VARIABLE),
                new MemberEntry("stderr", "stream", CompletionItem.KIND_VARIABLE)
        ));

        map.put("math", Arrays.asList(
                new MemberEntry("pi", "float", CompletionItem.KIND_VARIABLE),
                new MemberEntry("e", "float", CompletionItem.KIND_VARIABLE),
                new MemberEntry("sin", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("cos", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("sqrt", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("ceil", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("json", Arrays.asList(
                new MemberEntry("dump", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("dumps", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("load", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("loads", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("re", Arrays.asList(
                new MemberEntry("compile", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("search", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("match", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("findall", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("sub", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("pathlib", Arrays.asList(
                new MemberEntry("Path", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("PurePath", "class", CompletionItem.KIND_CLASS)
        ));

        map.put("typing", Arrays.asList(
                new MemberEntry("Any", "type", CompletionItem.KIND_CLASS),
                new MemberEntry("Optional", "type", CompletionItem.KIND_CLASS),
                new MemberEntry("List", "type", CompletionItem.KIND_CLASS),
                new MemberEntry("Dict", "type", CompletionItem.KIND_CLASS),
                new MemberEntry("Tuple", "type", CompletionItem.KIND_CLASS)
        ));

        map.put("numpy", Arrays.asList(
                new MemberEntry("array", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("zeros", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("ones", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("arange", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("linspace", "function", CompletionItem.KIND_FUNCTION)
        ));

        map.put("requests", Arrays.asList(
                new MemberEntry("get", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("post", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("Session", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("Response", "class", CompletionItem.KIND_CLASS)
        ));

        map.put("httpx", Arrays.asList(
                new MemberEntry("get", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("post", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("put", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("delete", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("request", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("stream", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("Client", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("AsyncClient", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("Response", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("Request", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("Timeout", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("URL", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("Headers", "class", CompletionItem.KIND_CLASS),
                new MemberEntry("codes", "module", CompletionItem.KIND_MODULE)
        ));

        map.put("six", Arrays.asList(
                new MemberEntry("PY2", "bool", CompletionItem.KIND_VARIABLE),
                new MemberEntry("PY3", "bool", CompletionItem.KIND_VARIABLE),
                new MemberEntry("iteritems", "function", CompletionItem.KIND_FUNCTION),
                new MemberEntry("itervalues", "function", CompletionItem.KIND_FUNCTION)
        ));

        return map;
    }
}
