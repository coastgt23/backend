using Stella.Models;

namespace Stella.Routes.API
{
    public class Config
    {
        [ServerAPI.GET("/api/config/v1/amplitude")]
        public dynamic Amplitude()
        {
            return new AmplitudeConfig
            {
                AmplitudeKey = "98b7a8604b19f3c703ddf09d3427fd0a"
            };
        }

        // Returns the four "What would you like to do first?" buttons shown in the OOBE
        // (RegistrationSceneNUX → ChooseCohortScreen). Each button maps to a room the new
        // player can teleport into. Wire shape per CohortNUXButtonConfig: all four entries
        // below use Override=0 (None), so ChooseCohortScreen resolves DefaultRoomName via
        // Rooms.GetByName rather than reading CustomRoomName/CustomTitle — those two fields
        // are left empty since they're unused in that path. Room names below are Rec Room
        // Original rooms already present in the seeded hotlist (RecCenter, Paintball,
        // Dodgeball, GoldenTrophy), chosen as the most newcomer-friendly of that set.
        // `cohortId` isn't read yet — every cohort currently gets the same four buttons.
        // If cohort-specific button sets are needed later, branch on it here.
        [ServerAPI.GET("/api/config/v1/cohortnux/{cohortId}")]
        public dynamic GetCohortNux(int cohortId)
        {
            return new[]
            {
                new
                {
                    Version = 1, ButtonNumber = 0, Override = 0,
                    CustomRoomName = "", CustomTitle = "",
                    DefaultRoomName = "RecCenter",
                    DefaultTitle = "Hang out at the Rec Center",
                },
                new
                {
                    Version = 1, ButtonNumber = 1, Override = 0,
                    CustomRoomName = "", CustomTitle = "",
                    DefaultRoomName = "Paintball",
                    DefaultTitle = "Play Paintball",
                },
                new
                {
                    Version = 1, ButtonNumber = 2, Override = 0,
                    CustomRoomName = "", CustomTitle = "",
                    DefaultRoomName = "Dodgeball",
                    DefaultTitle = "Play Dodgeball",
                },
                new
                {
                    Version = 1, ButtonNumber = 3, Override = 0,
                    CustomRoomName = "", CustomTitle = "",
                    DefaultRoomName = "GoldenTrophy",
                    DefaultTitle = "Quest for the Golden Trophy",
                },
            };
        }

        [ServerAPI.GET("/api/config/v2")]
        [ServerAPI.UseAuthorization]
        public dynamic ConfigV2()
        {
            var config = new ConfigDTO
            {
                ShareBaseUrl = "https://stellaonline.org/",
                LevelProgressionMaps = [],
                DailyObjectives = [
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ],
                    [
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        },
                        new OldObjectiveDTO
                        {
                            Type = ObjectiveType.StuntRunnerWins,
                            Score = 1,
                        }
                    ]
                ],
                AutoMicMutingConfig = new AutoMicMutingConfigDTO
                {
                    MicSpamVolumeThreshold = 0,
                    MicVolumeSampleInterval = 0,
                    MicVolumeSampleRollingWindowLength = 0,
                    MicSpamSamplePercentageForWarning = 0,
                    MicSpamSamplePercentageForWarningToEnd = 0,
                    MicSpamSamplePercentageForForceMute = 0,
                    MicSpamSamplePercentageForForceMuteToEnd = 0,
                    MicSpamWarningStateVolumeMultiplier = 0
                },
            };

            return config;
        }
    }
}