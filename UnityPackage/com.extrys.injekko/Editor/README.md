Injekko editor-side code lives here. The package now includes graph-plan compilation, scene cache refresh hooks and the first graph authoring inspectors.
Editor-side utilities currently included in the package:

- `Tools/Injekko/Validate Setup`
- `Tools/Injekko/Compile Graph Plans`
- `Tools/Injekko/Write Graph Report`

Graph Toolkit is now also referenced directly by the package for iteration. A first real editor graph prototype can be created from:

- `Assets/Create/Injekko/Graph Toolkit/Injekko Authoring Graph`

`Write Graph Report` depends on the generated `Injekko_GraphMetadata.Create()` type existing in the consumer assembly.
