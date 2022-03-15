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

using System.Collections.Generic;
using System.Linq;

namespace Metrics.Shared
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class DeveloperLogEvent
    {
        public class Types
        {
            public class ExternalToolError
            {
                public int? ExitCode { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as ExternalToolError);

                public bool Equals(ExternalToolError other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.ExitCode != ExitCode)
                    {
                        return false;
                    }

                    return true;
                }

                public ExternalToolError Clone() => (ExternalToolError) MemberwiseClone();

                public void MergeFrom(ExternalToolError other)
                {
                    if (other.ExitCode.HasValue)
                    {
                        ExitCode = other.ExitCode;
                    }
                }
            }

            public class CommandData
            {
                public int? ResultsCount { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as CommandData);

                public bool Equals(CommandData other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.ResultsCount != ResultsCount)
                    {
                        return false;
                    }

                    return true;
                }

                public CommandData Clone() => (CommandData) MemberwiseClone();

                public void MergeFrom(CommandData other)
                {
                    if (other.ResultsCount.HasValue)
                    {
                        ResultsCount = other.ResultsCount;
                    }
                }
            }

            public class GameletData
            {
                public class Types
                {
                    public enum Type
                    {
                        GameletTypeUnknown,
                        GameletDevkit,
                        GameletEdge,
                    }
                }

                public Types.Type? Type { get; set; }
                public int? State { get; set; }
                public string Zone { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as GameletData);

                public bool Equals(GameletData other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.Type != Type)
                    {
                        return false;
                    }

                    if (other.State != State)
                    {
                        return false;
                    }

                    if (other.Zone != Zone)
                    {
                        return false;
                    }

                    return true;
                }

                public GameletData Clone() => (GameletData) MemberwiseClone();

                public void MergeFrom(GameletData other)
                {
                    if (other.Type.HasValue)
                    {
                        Type = other.Type;
                    }

                    if (other.State.HasValue)
                    {
                        State = other.State;
                    }

                    if (other.Zone != null)
                    {
                        Zone = other.Zone;
                    }
                }
            }

            public class LoadSymbolData
            {
                public int? FlatSymbolStoresCount { get; set; }
                public int? StructuredSymbolStoresCount { get; set; }
                public int? HttpSymbolStoresCount { get; set; }
                public int? StadiaSymbolStoresCount { get; set; }
                public int? ModulesCount { get; set; }
                public int? ModulesBeforeCount { get; set; }
                public int? ModulesAfterCount { get; set; }
                public int? ModulesWithSymbolsLoadedBeforeCount { get; set; }
                public int? ModulesWithSymbolsLoadedAfterCount { get; set; }
                public int? BinariesLoadedBeforeCount { get; set; }
                public int? BinariesLoadedAfterCount { get; set; }
                public int? SymbolCountAfter { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as LoadSymbolData);

                public bool Equals(LoadSymbolData other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.FlatSymbolStoresCount != FlatSymbolStoresCount)
                    {
                        return false;
                    }

                    if (other.StructuredSymbolStoresCount != StructuredSymbolStoresCount)
                    {
                        return false;
                    }

                    if (other.HttpSymbolStoresCount != HttpSymbolStoresCount)
                    {
                        return false;
                    }

                    if (other.StadiaSymbolStoresCount != StadiaSymbolStoresCount)
                    {
                        return false;
                    }

                    if (other.ModulesCount != ModulesCount)
                    {
                        return false;
                    }

                    if (other.ModulesBeforeCount != ModulesBeforeCount)
                    {
                        return false;
                    }

                    if (other.ModulesAfterCount != ModulesAfterCount)
                    {
                        return false;
                    }

                    if (other.ModulesWithSymbolsLoadedBeforeCount !=
                        ModulesWithSymbolsLoadedBeforeCount)
                    {
                        return false;
                    }

                    if (other.ModulesWithSymbolsLoadedAfterCount !=
                        ModulesWithSymbolsLoadedAfterCount)
                    {
                        return false;
                    }

                    if (other.BinariesLoadedBeforeCount != BinariesLoadedBeforeCount)
                    {
                        return false;
                    }

                    if (other.BinariesLoadedAfterCount != BinariesLoadedAfterCount)
                    {
                        return false;
                    }

                    if (other.SymbolCountAfter != SymbolCountAfter)
                    {
                        return false;
                    }

                    return true;
                }

                public LoadSymbolData Clone() => (LoadSymbolData) MemberwiseClone();

                public void MergeFrom(LoadSymbolData other)
                {
                    if (other.FlatSymbolStoresCount.HasValue)
                    {
                        FlatSymbolStoresCount = other.FlatSymbolStoresCount;
                    }

                    if (other.StructuredSymbolStoresCount.HasValue)
                    {
                        StructuredSymbolStoresCount = other.StructuredSymbolStoresCount;
                    }

                    if (other.HttpSymbolStoresCount.HasValue)
                    {
                        HttpSymbolStoresCount = other.HttpSymbolStoresCount;
                    }

                    if (other.StadiaSymbolStoresCount.HasValue)
                    {
                        StadiaSymbolStoresCount = other.StadiaSymbolStoresCount;
                    }

                    if (other.ModulesCount.HasValue)
                    {
                        ModulesCount = other.ModulesCount;
                    }

                    if (other.ModulesBeforeCount.HasValue)
                    {
                        ModulesBeforeCount = other.ModulesBeforeCount;
                    }

                    if (other.ModulesAfterCount.HasValue)
                    {
                        ModulesAfterCount = other.ModulesAfterCount;
                    }

                    if (other.ModulesWithSymbolsLoadedBeforeCount.HasValue)
                    {
                        ModulesWithSymbolsLoadedBeforeCount =
                            other.ModulesWithSymbolsLoadedBeforeCount;
                    }

                    if (other.ModulesWithSymbolsLoadedAfterCount.HasValue)
                    {
                        ModulesWithSymbolsLoadedAfterCount =
                            other.ModulesWithSymbolsLoadedAfterCount;
                    }

                    if (other.BinariesLoadedBeforeCount.HasValue)
                    {
                        BinariesLoadedBeforeCount = other.BinariesLoadedBeforeCount;
                    }

                    if (other.BinariesLoadedAfterCount.HasValue)
                    {
                        BinariesLoadedAfterCount = other.BinariesLoadedAfterCount;
                    }

                    if (other.SymbolCountAfter.HasValue)
                    {
                        SymbolCountAfter = other.SymbolCountAfter;
                    }
                }
            }

            public enum LatencyType
            {
                UnknownLatencyType,
                LatencyTool,
                LatencyUser,
            }
        }

        public DeveloperApiEndpoint.Types.Type? ApiEndpoint { get; set; }
        public BinaryType.Types.Type? BinaryType { get; set; }
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string VisualStudioVersion { get; set; }
        public string SdkVersion { get; set; }
        public string VsExtensionVersion { get; set; }
        public string VsSessionIdStr { get; set; }
        public string DebugSessionIdStr { get; set; }
        public DeveloperEventStatus.Types.Code? StatusCode { get; set; }
        public GrpcServiceCallDetails GrpcErrorDetails { get; set; }
        public Types.ExternalToolError ExternalToolError { get; set; }
        public long? LatencyMilliseconds { get; set; }

        public Types.LatencyType? LatencyType { get; set; }

        public Types.CommandData CommandData { get; set; }

        public Types.GameletData GameletData { get; set; }
        public List<GrpcServiceCallDetails> GrpcCallDetails { get; set; }

        public Types.LoadSymbolData LoadSymbolData { get; set; }
        public DebugPreflightCheckData DebugPreflightCheckData { get; set; }
        public DebugSessionEndData DebugSessionEndData { get; set; }
        public VSIDebugEventBatch DebugEventBatch { get; set; }
        public VSIDebugParameters DebugParameters { get; set; }
        public CopyBinaryData CopyExecutable { get; set; }
        public CopyBinaryData CopyLldbServer { get; set; }
        public CustomCommandData CustomCommand { get; set; }
        public List<VSIExceptionData> ExceptionsData { get; set; }
        public VSIBoundBreakpointsData BoundBreakpointsData { get; set; }
        public GameLaunchData GameLaunchData { get; set; }
        public VSIDebugExpressionEvaluationBatch DebugExpressionEvaluationBatch { get; set; }
        public VSIProjectProperties ProjectProperties { get; set; }

        public DeveloperLogEvent()
        {
            GrpcCallDetails = new List<GrpcServiceCallDetails>();
            ExceptionsData = new List<VSIExceptionData>();
        }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as DeveloperLogEvent);

        public bool Equals(DeveloperLogEvent other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.ApiEndpoint != ApiEndpoint)
            {
                return false;
            }

            if (other.BinaryType != BinaryType)
            {
                return false;
            }

            if (other.OrganizationId != OrganizationId)
            {
                return false;
            }

            if (other.ProjectId != ProjectId)
            {
                return false;
            }

            if (other.VisualStudioVersion != VisualStudioVersion)
            {
                return false;
            }

            if (other.SdkVersion != SdkVersion)
            {
                return false;
            }

            if (other.VsExtensionVersion != VsExtensionVersion)
            {
                return false;
            }

            if (other.VsSessionIdStr != VsSessionIdStr)
            {
                return false;
            }

            if (other.DebugSessionIdStr != DebugSessionIdStr)
            {
                return false;
            }

            if (other.StatusCode != StatusCode)
            {
                return false;
            }

            if (!Equals(other.GrpcErrorDetails, GrpcErrorDetails))
            {
                return false;
            }

            if (!Equals(other.ExternalToolError, ExternalToolError))
            {
                return false;
            }

            if (other.LatencyMilliseconds != LatencyMilliseconds)
            {
                return false;
            }

            if (other.LatencyType != LatencyType)
            {
                return false;
            }

            if (!Equals(other.CommandData, CommandData))
            {
                return false;
            }

            if (!Equals(other.GameletData, GameletData))
            {
                return false;
            }

            if (other.GrpcCallDetails != GrpcCallDetails &&
                (other.GrpcCallDetails == null || GrpcCallDetails == null) ||
                !other.GrpcCallDetails.SequenceEqual(GrpcCallDetails))
            {
                return false;
            }

            if (!Equals(other.LoadSymbolData, LoadSymbolData))
            {
                return false;
            }

            if (!Equals(other.DebugPreflightCheckData, DebugPreflightCheckData))
            {
                return false;
            }

            if (!Equals(other.DebugSessionEndData, DebugSessionEndData))
            {
                return false;
            }

            if (!Equals(other.DebugEventBatch, DebugEventBatch))
            {
                return false;
            }

            if (!Equals(other.DebugParameters, DebugParameters))
            {
                return false;
            }

            if (!Equals(other.CopyExecutable, CopyExecutable))
            {
                return false;
            }

            if (!Equals(other.CopyLldbServer, CopyLldbServer))
            {
                return false;
            }

            if (!Equals(other.CustomCommand, CustomCommand))
            {
                return false;
            }

            if (other.ExceptionsData != ExceptionsData &&
                (other.ExceptionsData == null || ExceptionsData == null) ||
                !other.ExceptionsData.SequenceEqual(ExceptionsData))
            {
                return false;
            }

            if (!Equals(other.BoundBreakpointsData, BoundBreakpointsData))
            {
                return false;
            }

            if (!Equals(other.GameLaunchData, GameLaunchData))
            {
                return false;
            }

            if (!Equals(other.DebugExpressionEvaluationBatch, DebugExpressionEvaluationBatch))
            {
                return false;
            }

            if (!Equals(other.ProjectProperties, ProjectProperties))
            {
                return false;
            }

            return true;
        }

        public DeveloperLogEvent Clone()
        {
            var clone = (DeveloperLogEvent)MemberwiseClone();
            clone.GrpcErrorDetails = GrpcErrorDetails?.Clone();
            clone.ExternalToolError = ExternalToolError?.Clone();
            clone.CommandData = CommandData?.Clone();
            clone.GameletData = GameletData?.Clone();
            clone.GrpcCallDetails = GrpcCallDetails?.Select(x => x.Clone()).ToList();
            clone.LoadSymbolData = LoadSymbolData?.Clone();
            clone.DebugPreflightCheckData = DebugPreflightCheckData?.Clone();
            clone.DebugSessionEndData = DebugSessionEndData?.Clone();
            clone.DebugEventBatch = DebugEventBatch?.Clone();
            clone.DebugParameters = DebugParameters?.Clone();
            clone.CopyExecutable = CopyExecutable?.Clone();
            clone.CopyLldbServer = CopyLldbServer?.Clone();
            clone.CustomCommand = CustomCommand?.Clone();
            clone.ExceptionsData = ExceptionsData?.Select(x => x.Clone()).ToList();
            clone.BoundBreakpointsData = BoundBreakpointsData?.Clone();
            clone.GameLaunchData = GameLaunchData?.Clone();
            clone.DebugExpressionEvaluationBatch = DebugExpressionEvaluationBatch?.Clone();
            clone.ProjectProperties = ProjectProperties?.Clone();
            return clone;
        }

        public void MergeFrom(DeveloperLogEvent other)
        {
            if (other == null)
            {
                return;
            }

            if (other.ApiEndpoint.HasValue)
            {
                ApiEndpoint = other.ApiEndpoint;
            }

            if (other.BinaryType.HasValue)
            {
                BinaryType = other.BinaryType;
            }

            if (other.OrganizationId != null)
            {
                OrganizationId = other.OrganizationId;
            }

            if (other.ProjectId != null)
            {
                ProjectId = other.ProjectId;
            }

            if (other.VisualStudioVersion != null)
            {
                VisualStudioVersion = other.VisualStudioVersion;
            }

            if (other.SdkVersion != null)
            {
                SdkVersion = other.SdkVersion;
            }

            if (other.VsExtensionVersion != null)
            {
                VsExtensionVersion = other.VsExtensionVersion;
            }

            if (other.VsSessionIdStr != null)
            {
                VsSessionIdStr = other.VsSessionIdStr;
            }

            if (other.DebugSessionIdStr != null)
            {
                DebugSessionIdStr = other.DebugSessionIdStr;
            }

            if (other.StatusCode.HasValue)
            {
                StatusCode = other.StatusCode;
            }

            if (other.GrpcErrorDetails != null)
            {
                if (GrpcErrorDetails == null)
                {
                    GrpcErrorDetails = new GrpcServiceCallDetails();
                }

                GrpcErrorDetails.MergeFrom(other.GrpcErrorDetails);
            }

            if (other.ExternalToolError != null)
            {
                if (ExternalToolError == null)
                {
                    ExternalToolError = new Types.ExternalToolError();
                }

                ExternalToolError.MergeFrom(other.ExternalToolError);
            }

            if (other.LatencyMilliseconds.HasValue)
            {
                LatencyMilliseconds = other.LatencyMilliseconds;
            }

            if (other.LatencyType.HasValue)
            {
                LatencyType = other.LatencyType;
            }

            if (other.CommandData != null)
            {
                if (CommandData == null)
                {
                    CommandData = new Types.CommandData();
                }

                CommandData.MergeFrom(other.CommandData);
            }

            if (other.GameletData != null)
            {
                if (GameletData == null)
                {
                    GameletData = new Types.GameletData();
                }

                GameletData.MergeFrom(other.GameletData);
            }

            if (other.GrpcCallDetails != null)
            {
                if (GrpcCallDetails == null)
                {
                    GrpcCallDetails = new List<GrpcServiceCallDetails>();
                }

                GrpcCallDetails.AddRange(other.GrpcCallDetails);
            }

            if (other.LoadSymbolData != null)
            {
                if (LoadSymbolData == null)
                {
                    LoadSymbolData = new Types.LoadSymbolData();
                }

                LoadSymbolData.MergeFrom(other.LoadSymbolData);
            }

            if (other.DebugPreflightCheckData != null)
            {
                if (DebugPreflightCheckData == null)
                {
                    DebugPreflightCheckData = new DebugPreflightCheckData();
                }

                DebugPreflightCheckData.MergeFrom(other.DebugPreflightCheckData);
            }

            if (other.DebugSessionEndData != null)
            {
                if (DebugSessionEndData == null)
                {
                    DebugSessionEndData = new DebugSessionEndData();
                }

                DebugSessionEndData.MergeFrom(other.DebugSessionEndData);
            }

            if (other.DebugEventBatch != null)
            {
                if (DebugEventBatch == null)
                {
                    DebugEventBatch = new VSIDebugEventBatch();
                }

                DebugEventBatch.MergeFrom(other.DebugEventBatch);
            }

            if (other.DebugParameters != null)
            {
                if (DebugParameters == null)
                {
                    DebugParameters = new VSIDebugParameters();
                }

                DebugParameters.MergeFrom(other.DebugParameters);
            }

            if (other.CopyExecutable != null)
            {
                if (CopyExecutable == null)
                {
                    CopyExecutable = new CopyBinaryData();
                }

                CopyExecutable.MergeFrom(other.CopyExecutable);
            }

            if (other.CopyLldbServer != null)
            {
                if (CopyLldbServer == null)
                {
                    CopyLldbServer = new CopyBinaryData();
                }

                CopyLldbServer.MergeFrom(other.CopyLldbServer);
            }

            if (other.CustomCommand != null)
            {
                if (CustomCommand == null)
                {
                    CustomCommand = new CustomCommandData();
                }

                CustomCommand.MergeFrom(other.CustomCommand);
            }

            if (other.ExceptionsData != null)
            {
                if (ExceptionsData == null)
                {
                    ExceptionsData = new List<VSIExceptionData>();
                }

                ExceptionsData.AddRange(other.ExceptionsData);
            }

            if (other.BoundBreakpointsData != null)
            {
                if (BoundBreakpointsData == null)
                {
                    BoundBreakpointsData = new VSIBoundBreakpointsData();
                }

                BoundBreakpointsData.MergeFrom(other.BoundBreakpointsData);
            }

            if (other.GameLaunchData != null)
            {
                if (GameLaunchData == null)
                {
                    GameLaunchData = new GameLaunchData();
                }

                GameLaunchData.MergeFrom(other.GameLaunchData);
            }

            if (other.DebugExpressionEvaluationBatch != null)
            {
                if (DebugExpressionEvaluationBatch == null)
                {
                    DebugExpressionEvaluationBatch = new VSIDebugExpressionEvaluationBatch();
                }

                DebugExpressionEvaluationBatch.MergeFrom(other.DebugExpressionEvaluationBatch);
            }

            if (other.ProjectProperties != null)
            {
                if (ProjectProperties == null)
                {
                    ProjectProperties = new VSIProjectProperties();
                }

                ProjectProperties.MergeFrom(other.ProjectProperties);
            }
        }
    }
}
