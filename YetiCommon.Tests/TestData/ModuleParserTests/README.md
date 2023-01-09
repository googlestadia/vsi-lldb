# ModuleParserTests binaries

The ModuleParserTests parse different binary test files. To prevent detection
of binaries in the code base, and accidental execution, the files have been
scrambled by applying b -> 255-b for every byte and named `.dat`.