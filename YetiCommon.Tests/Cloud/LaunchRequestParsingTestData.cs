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
using GgpGrpc.Models;

namespace YetiCommon.Tests.Cloud
{
    public static class LaunchRequestParsingTestData
    {
        public static ChromeClientLauncher.Params ValidParams =>
            new ChromeClientLauncher.Params
            {
                Account = "some_account",
                ApplicationName = "test/app",
                Cmd = "some_bin arg1",
                Debug = true,
                GameletEnvironmentVars = "Var1=1;vAR2=3",
                GameletName = "test/gamelet",
                PoolId = "test_pool",
                RenderDoc = true,
                Rgp = false,
                SdkVersion = "1",
                VulkanDriverVariant = "test_variant",
                SurfaceEnforcementMode = SurfaceEnforcementSetting.Warn,
                TestAccount = "some_test_account",
                QueryParams = ""
            };

        public static LaunchGameRequest ValidRequest => new LaunchGameRequest
        {
            Parent = "Request_parent",
            GameletName = "Request_gamelet_name",
            ApplicationName = "Request_app_name",
            ExecutablePath = "Request_bin",
            CommandLineArguments = new[] { "Request_arg1", "--arg456" },
            EnvironmentVariablePairs = new Dictionary<string, string>
                { { "Request_VAR1", "Some value" } },
            SurfaceEnforcementMode = SurfaceEnforcementSetting.Block,
            Debug = false
        };

        public static Dictionary<string, string> IgnoreQueryParams =>
            new Dictionary<string, string>
            {
                { "account", "params_account" },
                { "gamelet_id", "params_gamelet_id" },
                { "application_id", "params_app_id" },
                { "application_version", "params_app_version" },
                { "gamelet_name", "params_gamelet_name" },
                { "package_id", "params_package_id" },
                { "sdk_version", "params_sdk_version" },
                { "headless", "params_headless" },
                { "game_launch_name", "params_game_launch_name" }
            };

        public static Dictionary<string, string> ValidParamsQueryParams =>
            new Dictionary<string, string>
            {
                { "test_account", "params_test_account" },
                { "cmd", "  some_bin arg2" },
                { "vars", "ParamsVar=val" },
                { "renderdoc", "false" },
                { "rgp", "1" }
            };

        public static Dictionary<string, string> ValidRequestQueryParams =>
            new Dictionary<string, string>
            {
                { "application_name", "params_app_name" },
                { "igd", "23475456543" },
                { "game_state_id", "params_game_state_name" },
                { "client_resolution", "720p" },
                { "dynamic_range_type", "SDR" },
                { "video_codec", "VP9" },
                { "audio_channel_mode", "SURROUND51" },
                { "debug_mode", "true" },
                { "start_forward_frame_dump", "0" },
                { "streamer_fixed_resolution", "1440p" },
                { "streamer_fixed_fps", "765" },
                { "streamer_maximum_bandwidth_kbps", "8764" },
                { "streamer_minimum_bandwidth_kbps", "23" },
                { "surface_enforcement_mode", "WARN" },
                { "enforce_production_ram", "False" },
                { "enable_realtime_priority", "1" },
                { "mount_uploaded_pipeline_cache", "True" },
                { "enable_pipeline_cache_source_upload", "false" },
                { "mount_dynamic_content", "true" },
                { "add_instance_compatibility_requirements", "r1,other" },
                { "remove_instance_compatibility_requirements", "5,7, 8" },
                { "package_name", "params_package_name" },
                { "enable_retroactive_frame_dump", "True" },
                { "stream_profile_preset", "HIGH_VISUAL_QUALITY" },
                { "pixel_density", "9876" },
            };

        public static Dictionary<string, string> OtherQueryParams =>
            new Dictionary<string, string>
            {
                { "pool_name", "other_pool_name" },
                { "playable_toast", "other_playable_toast" },
                { "boreal", "other_boreal" },
                { "enable_idle_timeout", "other_enable_idle_timeout" },
                { "enable_text_entry", "other_enable_text_entry" },
                { "enable_keyboard", "other_enable_keyboard" },
                { "enable_mouse", "other_enable_mouse" },
                { "enable_touch", "other_enable_touch" },
                { "enable_auto_focus", "other_enable_auto_focus" },
                { "emulated_gamepad_type", "other_emulated_gamepad_type" },
                { "session_id", "other_session_id" },
                { "gamelet_ip", "other_gamelet_ip" },
                { "gamelet_port", "other_gamelet_port" },
                { "enable_auth", "other_enable_auth" },
                { "event_interceptor", "other_event_interceptor" },
                { "force_enable_pointer_lock", "other_force_enable_pointer_lock" },
                { "enable_chrome_extension_mode", "other_enable_chrome_extension_mode" },
                { "= _&?%;,.´ßüöäÖяї", " &%$`=+%?_;.,ПЛЇ" }
            };

        public static Dictionary<string, string> AllValidQueryParams =>
            ValidParamsQueryParams.ToList().Concat(IgnoreQueryParams)
                .Concat(ValidRequestQueryParams).Concat(OtherQueryParams)
                .ToDictionary(p => p.Key, p => p.Value);
    }
}
