// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    // Generic actions that can be recorded using metrics.
    public enum ActionType
    {
        ApplicationGet,
        CrashDumpAttach,
        CrashDumpDownload,
        CrashDumpList,
        DebugAttach,
        DebugContinueAfterAttach,
        DebugCreateExecutableTarget,
        DebugEngineLoadSymbols,
        DebugLoadCore,
        DebugModuleLoadSymbols,
        DebugPreflightBinaryChecks,
        DebugSetupQueries,
        DebugStart,
        DebugEnd,
        DebugWaitDebugger,
        DebugWaitProcess,
        GameletClearLogs,
        GameletEnableSsh,
        GameletGet,
        GameletsList,
        GameletPrepare,
        GameletReadMounts,
        GameletSelect,
        GameletStop,
        ProcessList,
        RemoteDeploy,
        LldbServerDeploy,
        GameLaunchStopPrompt,
        GameLaunchDeleteExisting,
        GameLaunchWaitForStart,
        GameLaunchCreate,
        GameLaunchWaitForEnd,
        GameLaunchGetExisting,
        ReportFeedback,
    }

    class ActionTypeMapping
    {
        public static DeveloperEventType.Types.Type ActionToEventType(ActionType type)
        {
            switch (type)
            {
                case ActionType.ApplicationGet:
                    return DeveloperEventType.Types.Type.VsiApplicationsGet;
                case ActionType.CrashDumpAttach:
                    return DeveloperEventType.Types.Type.VsiCoreAttach;
                case ActionType.CrashDumpDownload:
                    return DeveloperEventType.Types.Type.VsiCoreDownload;
                case ActionType.CrashDumpList:
                    return DeveloperEventType.Types.Type.VsiCoreList;
                case ActionType.DebugAttach:
                    return DeveloperEventType.Types.Type.VsiDebugAttach;
                case ActionType.DebugContinueAfterAttach:
                    return DeveloperEventType.Types.Type.VsiDebugContinueAfterAttach;
                case ActionType.DebugEngineLoadSymbols:
                    return DeveloperEventType.Types.Type.VsiDebugEngineLoadSymbols;
                case ActionType.DebugLoadCore:
                    return DeveloperEventType.Types.Type.VsiDebugLoadCore;
                case ActionType.DebugModuleLoadSymbols:
                    return DeveloperEventType.Types.Type.VsiDebugModuleLoadSymbols;
                case ActionType.DebugSetupQueries:
                    return DeveloperEventType.Types.Type.VsiDebugSetupQueries;
                case ActionType.DebugStart:
                    return DeveloperEventType.Types.Type.VsiDebugStart;
                case ActionType.DebugEnd:
                    return DeveloperEventType.Types.Type.VsiDebugEnd;
                case ActionType.DebugWaitDebugger:
                    return DeveloperEventType.Types.Type.VsiDebugWaitDebugger;
                case ActionType.DebugCreateExecutableTarget:
                    return DeveloperEventType.Types.Type.VsiDebugCreateExecutableTarget;
                case ActionType.DebugWaitProcess:
                    return DeveloperEventType.Types.Type.VsiDebugWaitProcess;
                case ActionType.GameletClearLogs:
                    return DeveloperEventType.Types.Type.VsiGameletsClearLogs;
                case ActionType.GameletEnableSsh:
                    return DeveloperEventType.Types.Type.VsiGameletsEnableSsh;
                case ActionType.GameletGet:
                    return DeveloperEventType.Types.Type.VsiGameletsGet;
                case ActionType.GameletsList:
                    return DeveloperEventType.Types.Type.VsiGameletsList;
                case ActionType.GameletPrepare:
                    return DeveloperEventType.Types.Type.VsiGameletsPrepare;
                case ActionType.GameletReadMounts:
                    return DeveloperEventType.Types.Type.VsiGameletsReadMounts;
                case ActionType.GameletSelect:
                    return DeveloperEventType.Types.Type.VsiGameletsSelect;
                case ActionType.GameletStop:
                    return DeveloperEventType.Types.Type.VsiGameletsStop;
                case ActionType.ProcessList:
                    return DeveloperEventType.Types.Type.VsiProcessList;
                case ActionType.RemoteDeploy:
                    return DeveloperEventType.Types.Type.VsiDeployBinary;
                case ActionType.LldbServerDeploy:
                    return DeveloperEventType.Types.Type.VsiDeployLldbServer;
                case ActionType.DebugPreflightBinaryChecks:
                    return DeveloperEventType.Types.Type.VsiDebugPreflightBinaryCheck;
                case ActionType.GameLaunchStopPrompt:
                    return DeveloperEventType.Types.Type.VsiGameLaunchStopPrompt;
                case ActionType.GameLaunchDeleteExisting:
                    return DeveloperEventType.Types.Type.VsiGameLaunchDeleteExisting;
                case ActionType.GameLaunchWaitForStart:
                    return DeveloperEventType.Types.Type.VsiGameLaunchWaitForStart;
                case ActionType.GameLaunchCreate:
                    return DeveloperEventType.Types.Type.VsiGameLaunchCreate;
                case ActionType.GameLaunchWaitForEnd:
                    return DeveloperEventType.Types.Type.VsiGameLaunchWaitForEnd;
                case ActionType.GameLaunchGetExisting:
                    return DeveloperEventType.Types.Type.VsiGameLaunchGetExisting;
                case ActionType.ReportFeedback:
                    return DeveloperEventType.Types.Type.VsiReportFeedback;
            }
            return DeveloperEventType.Types.Type.UnknownEventType;
        }
    }
}
