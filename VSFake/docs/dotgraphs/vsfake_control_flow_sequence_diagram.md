```sequence-diagram
participant Test
participant VSFake
participant VSI
participant LLDB

Note left of Test: (1)

Note left of VSFake: ProgramState=AtBreak [fillcolor="black", fontcolor="white", color="cyan"]

Test -> VSFake: _vsFake.Continue()
VSFake -> VSI: ...
VSI --> VSFake:
VSFake --> Test:

Note left of VSFake: ProgramState=Running [fillcolor="black", fontcolor="white", color="cyan"]

Test -> VSFake: _vsFake.RunUntilBreak()
LLDB -> VSI: Break hit [color="red"]
VSI -> VSFake: IDebugEventCallback2.Event() [color="red"]

Note left of VSFake: JobQueue=\[ProgramStoppedJob] [fillcolor="black", fontcolor="white", color="cyan"]
VSFake --> VSI: [color="red"]
VSI --> LLDB: [color="red"]

VSFake -> VSFake: ProgramStoppedJob.Run()
Note left of VSFake: ProgramState=AtBreak\nJobQueue=\[] [fillcolor="black", fontcolor="white", color="cyan"]

VSFake --> Test:

Note left of Test: (2)

Test -> VSFake: countWatch = _vsFake.AddWatch("count")
VSFake -> VSI: ...
VSI --> VSFake:
VSFake --> Test:

Note left of VSFake: countWatch.Ready=false\ncountWatch.State=VariableState.Pending\nJobQueue=\[RefreshVariableJob] [fillcolor="black", fontcolor="white", color="cyan"]

Test -> VSFake: _vsFake.RunUntil(() => countWatch.Ready))
VSFake -> VSFake: RefreshVariableJob.Run()

Note left of VSFake: countWatch.Ready=true\ncountWatch.State=VariableState.Evaluated\nJobQueue=\[] [fillcolor="black", fontcolor="white", color="cyan"]

VSFake --> Test:


Note left of Test: (3)
```