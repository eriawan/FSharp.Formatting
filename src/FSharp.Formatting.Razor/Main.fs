﻿namespace FSharp.Formatting.Razor

open FSharp.MetadataFormat
open FSharp.Formatting
open FSharp.Literate
open FSharp.Formatting.Common
open System.IO


/// This type exposes the functionality for producing documentation from `dll` files with associated `xml` files
/// generated by the F# or C# compiler using Razor templates. To generate documentation, use one of the overloades of the `Generate` method.
/// The overloads have the following optional parameters:
///
///  - `outDir` - specifies the output directory where documentation should be placed
///  - `layoutRoots` - a list of paths where Razor templates can be found
///  - `parameters` - provides additional parameters to the Razor templates
///  - `xmlFile` - can be used to override the default name of the XML file (by default, we assume
///     the file has the same name as the DLL)
///  - `markDownComments` - specifies if you want to use the Markdown parser for in-code comments.
///    With `markDownComments` enabled there is no support for `<see cref="">` links, so `false` is
///    recommended for C# assemblies (if not specified, `true` is used).
///  - `typeTemplate` - the templates to be used for normal types (and C# types)
///    (if not specified, `"type.cshtml"` is used).
///  - `moduleTemplate` - the templates to be used for modules
///    (if not specified, `"module.cshtml"` is used).
///  - `namespaceTemplate` - the templates to be used for namespaces
///    (if not specified, `"namespaces.cshtml"` is used).
///  - `assemblyReferences` - The assemblies to use when compiling Razor templates.
///    Use this parameter if templates fail to compile with `mcs` on Linux or Mac or
///    if you need additional references in your templates
///    (if not specified, we use the currently loaded assemblies).
///  - `sourceFolder` and `sourceRepo` - When specified, the documentation generator automatically
///    generates links to GitHub pages for each of the entity.
///  - `publicOnly` - When set to `false`, the tool will also generate documentation for non-public members
///  - `libDirs` - Use this to specify additional paths where referenced DLL files can be found
///  - `otherFlags` - Additional flags that are passed to the F# compiler (you can use this if you want to
///    specify references explicitly etc.)
///  - `urlRangeHighlight` - A function that can be used to override the default way of generating GitHub links
///
type RazorMetadataFormat private() =

  static let generate namespaceTemplate moduleTemplate typeTemplate layoutRoots outDir assemblyReferences generatorOutput =
    let (@@) a b = Path.Combine(a, b)

    let namespaceTemplate = defaultArg namespaceTemplate "namespaces.cshtml"
    let moduleTemplate = defaultArg moduleTemplate "module.cshtml"
    let typeTemplate = defaultArg typeTemplate "type.cshtml"

    let asm = generatorOutput.AssemblyGroup
    let props = generatorOutput.Properties
    let moduleInfos = generatorOutput.ModuleInfos
    let typesInfos = generatorOutput.TypesInfos

    // Generate all the HTML stuff
    Log.infof "Starting razor engine"
    let razor = RazorRender<AssemblyGroup>(layoutRoots, ["FSharp.MetadataFormat"], namespaceTemplate, ?references = assemblyReferences)

    Log.infof "Generating: index.html"
    let out = razor.ProcessFile(asm, props)
    File.WriteAllText(outDir @@ "index.html", out)

    // Generate documentation for all modules
    Log.infof "Generating modules..."
    let razor = RazorRender<ModuleInfo>(layoutRoots, ["FSharp.MetadataFormat"], moduleTemplate, ?references = assemblyReferences)

    for modulInfo in moduleInfos do
      Log.infof "Generating module: %s" modulInfo.Module.UrlName
      let out = razor.ProcessFile(modulInfo, props)
      File.WriteAllText(outDir @@ (modulInfo.Module.UrlName + ".html"), out)
      Log.infof "Finished module: %s" modulInfo.Module.UrlName

    Log.infof "Generating types..."

    // Generate documentation for all types
    let razor = new RazorRender<TypeInfo>(layoutRoots, ["FSharp.MetadataFormat"], typeTemplate, ?references = assemblyReferences)

    for typInfo in typesInfos do
      Log.infof "Generating type: %s" typInfo.Type.UrlName
      let out = razor.ProcessFile(typInfo, props)
      File.WriteAllText(outDir @@ (typInfo.Type.UrlName + ".html"), out)
      Log.infof "Finished type: %s" typInfo.Type.UrlName


  static member Generate(dllFile : string, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight, ?assemblyReferences) =
    MetadataFormat.Generate(dllFile, ?parameters = parameters, ?xmlFile = xmlFile, ?sourceRepo = sourceRepo, ?sourceFolder = sourceFolder,
        ?publicOnly = publicOnly, ?libDirs = libDirs, ?otherFlags = otherFlags, ?markDownComments = markDownComments, ?urlRangeHighlight = urlRangeHighlight)
    |> generate namespaceTemplate moduleTemplate typeTemplate layoutRoots outDir assemblyReferences

  /// generates documentation for multiple files specified by the `dllFiles` parameter
  static member Generate(dllFiles : _ list, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight, ?assemblyReferences) =
     MetadataFormat.Generate(dllFiles, ?parameters = parameters, ?xmlFile = xmlFile, ?sourceRepo = sourceRepo, ?sourceFolder = sourceFolder,
        ?publicOnly = publicOnly, ?libDirs = libDirs, ?otherFlags = otherFlags, ?markDownComments = markDownComments, ?urlRangeHighlight = urlRangeHighlight)
    |> generate namespaceTemplate moduleTemplate typeTemplate layoutRoots outDir assemblyReferences

  /// This overload generates documentation for multiple files specified by the `dllFiles` parameter
  static member Generate(dllFiles : _ seq, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight, ?assemblyReferences) =
     MetadataFormat.Generate(dllFiles, ?parameters = parameters, ?xmlFile = xmlFile, ?sourceRepo = sourceRepo, ?sourceFolder = sourceFolder,
        ?publicOnly = publicOnly, ?libDirs = libDirs, ?otherFlags = otherFlags, ?markDownComments = markDownComments, ?urlRangeHighlight = urlRangeHighlight)
    |> generate namespaceTemplate moduleTemplate typeTemplate layoutRoots outDir assemblyReferences


  static member Generate(generatedMetadata:FSharp.MetadataFormat.GeneratorOutput, outDir, layoutRoots, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate,?assemblyReferences) =
        generate namespaceTemplate moduleTemplate typeTemplate layoutRoots outDir assemblyReferences generatedMetadata


type RazorLiterate private () =
  static let defaultOutput output input kind =
    match output, defaultArg kind OutputKind.Html with
    | Some out, _ -> out
    | _, OutputKind.Latex -> Path.ChangeExtension(input, "tex")
    | _, OutputKind.Html -> Path.ChangeExtension(input, "html")

  static let replaceParameters (contentTag:string) (parameters:seq<string * string>) input =
    match input with
    | None ->
        // If there is no template, return just document + tooltips
        let lookup = parameters |> dict
        lookup.[contentTag] + "\n\n" + lookup.["tooltips"]
    | Some input ->
        // First replace keys with some uglier keys and then replace them with values
        // (in case one of the keys appears in some other value)
        let id = System.Guid.NewGuid().ToString("d")
        let input = parameters |> Seq.fold (fun (html:string) (key, value) ->
          html.Replace("{" + key + "}", "{" + key + id + "}")) input
        let result = parameters |> Seq.fold (fun (html:string) (key, value) ->
          html.Replace("{" + key + id + "}", value)) input
        result


  static let generateFile references contentTag parameters templateOpt output layoutRoots =
    match templateOpt with
    | Some (file:string) when file.EndsWith("cshtml", true, System.Globalization.CultureInfo.InvariantCulture) ->
      let razor = RazorRender(layoutRoots |> Seq.toList, [], file, ?references = references)
      let props = [ "Properties", dict parameters ]
      let generated = razor.ProcessFile(props)
      File.WriteAllText(output, generated)
    | _ ->
      let templateOpt = templateOpt |> Option.map File.ReadAllText
      File.WriteAllText(output, replaceParameters contentTag parameters templateOpt)

  static member ProcessDocument
    ( doc, output, ?templateFile, ?format, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?replacements, ?layoutRoots, ?assemblyReferences) =
      let res = Literate.ProcessDocument(doc,output, ?format = format, ?prefix = prefix, ?lineNumbers = lineNumbers, ?includeSource = includeSource, ?generateAnchors = generateAnchors, ?replacements = replacements)
      generateFile assemblyReferences res.ContentTag res.Parameters templateFile output (defaultArg layoutRoots [])


  static member ProcessMarkdown
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?layoutRoots, ?generateAnchors,
      ?assemblyReferences, ?customizeDocument ) =

      let res = Literate.ProcessMarkdown(input ,?output = output, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                                         ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                                         ?replacements = replacements, ?customizeDocument = customizeDocument )
      generateFile assemblyReferences res.ContentTag res.Parameters templateFile (defaultOutput output input format) (defaultArg layoutRoots [])

  static member ProcessScriptFile
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots,
      ?generateAnchors, ?assemblyReferences, ?customizeDocument ) =
        let res = Literate.ProcessScriptFile(input ,?output = output, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                                             ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                                             ?replacements = replacements, ?customizeDocument = customizeDocument, ?fsiEvaluator = fsiEvaluator )
        generateFile assemblyReferences res.ContentTag res.Parameters templateFile (defaultOutput output input format) (defaultArg layoutRoots [])

  static member ProcessDirectory
    ( inputDirectory, ?templateFile, ?outputDirectory, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots, ?generateAnchors,
      ?assemblyReferences, ?processRecursive, ?customizeDocument  ) =
        let outputDirectory = defaultArg outputDirectory inputDirectory

        let res = Literate.ProcessDirectory(inputDirectory, outputDirectory, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                                             ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                                             ?replacements = replacements, ?customizeDocument = customizeDocument, ?processRecursive = processRecursive, ?fsiEvaluator = fsiEvaluator)

        res |> List.iter (fun (path, res) ->  generateFile assemblyReferences res.ContentTag res.Parameters templateFile path (defaultArg layoutRoots []))