# Debugger Grpc

This project contains the protobufs based off of the LLDB SB APIs.  These protobufs
are then used with GRPC to make RPCs.

## Updating existing protos
Since version 1.17 of the Grpc library it is not necessary to compile .proto to .cs files manually
anymore. Just modify the .proto files, they are recompiled automatically.

In order to use new functions added to `SB<object_name>Api.proto` file please make sure to update:
- `SB<object_name>.cs` in DebuggerGrpcClient.Interfaces and LldbApi
- `LLDB<object_name>.h` and its implementation in GgpVsi.DebugEngine.LLDBWorker
- `SB<object_name>RpcServiceImpl.cs` in DebuggerGrpcServer

## Adding a new proto
Create a new .proto file in the protos directory. Unload the DebuggerGrpc project, open
DebuggerGrpc.csproj and add the new proto there (see other <Protobuf> nodes). Save and reload
the project. That's it, the file will compile automatically.

Enjoy!
