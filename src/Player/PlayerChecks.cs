/*
Copyright (C) 2024 Dea Brcka

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Text.Json;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharpTimer;

public partial class SharpTimer
{
    public bool IsAllowedPlayer(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid ||
            !player.PawnIsAlive || playerTimers[player.Slot].IsNoclip) return false;

        var playerSlot = player.Slot;

        var teamNum = (CsTeam)player.TeamNum;

        var isAlive = player.PawnIsAlive;
        var isTeamValid = teamNum == CsTeam.CounterTerrorist || teamNum == CsTeam.Terrorist;

        var isTeamSpectatorOrNone = teamNum != CsTeam.Spectator && teamNum != CsTeam.None;
        var isConnected = connectedPlayers.ContainsKey(playerSlot) && playerTimers.ContainsKey(playerSlot);
        var isConnectedJS = !jumpStatsEnabled || playerJumpStats.ContainsKey(playerSlot);

        return isTeamValid && isTeamSpectatorOrNone && isConnected && isConnectedJS && isAlive;
    }

    private bool IsAllowedSpectator(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return false;

        var teamNum = (CsTeam)player.TeamNum;
        var isTeamValid = teamNum == CsTeam.Spectator;
        var isConnected = connectedPlayers.ContainsKey(player.Slot) && playerTimers.ContainsKey(player.Slot);
        var isObservingValid = player.Pawn?.Value!.ObserverServices?.ObserverTarget != null &&
                               specTargets.ContainsKey(player.Pawn.Value.ObserverServices.ObserverTarget.Index);

        return isTeamValid && isConnected && isObservingValid;
    }

    public bool IsAllowedClient(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid)
            return false;

        return true;
    }

    private async Task IsPlayerATester(string steamId64, int playerSlot)
    {
        try
        {
            var response = await httpClient.GetStringAsync(testerPersonalGifsSource);

            using (var jsonDocument = JsonDocument.Parse(response))
            {
                if (playerTimers.TryGetValue(playerSlot, out var playerTimer))
                {
                    playerTimer.IsTester = jsonDocument.RootElement.TryGetProperty(steamId64, out var steamData);

                    if (playerTimer.IsTester)
                    {
                        if (steamData.TryGetProperty("SmolGif", out var smolGifElement))
                            playerTimer.TesterSmolGif = smolGifElement.GetString() ?? "";

                        if (steamData.TryGetProperty("BigGif", out var bigGifElement))
                            playerTimer.TesterBigGif = bigGifElement.GetString() ?? "";
                    }
                }
                else
                {
                    SharpTimerError("Error in IsPlayerATester: player not on server anymore");
                }
            }
        }
        catch (Exception ex)
        {
            SharpTimerError($"Error in IsPlayerATester: {ex.Message}");
        }
    }

    private async Task<string> GetTesterBigGif(string steamId64)
    {
        try
        {
            var response = await httpClient.GetStringAsync(testerPersonalGifsSource);

            using (var jsonDocument = JsonDocument.Parse(response))
            {
                jsonDocument.RootElement.TryGetProperty(steamId64, out var steamData);

                if (steamData.TryGetProperty("BigGif", out var bigGifElement))
                    return bigGifElement.GetString() ?? "";
                return "";
            }
        }
        catch (Exception ex)
        {
            SharpTimerError($"Error in GetTesterBigGif: {ex.Message}");
            return "";
        }
    }

    private async Task<string> GetTesterSmolGif(string steamId64)
    {
        try
        {
            var response = await httpClient.GetStringAsync(testerPersonalGifsSource);

            using (var jsonDocument = JsonDocument.Parse(response))
            {
                jsonDocument.RootElement.TryGetProperty(steamId64, out var steamData);

                if (steamData.TryGetProperty("SmolGif", out var smolGifElement))
                    return smolGifElement.GetString() ?? "";
                return "";
            }
        }
        catch (Exception ex)
        {
            SharpTimerError($"Error in GetTesterSmolGif: {ex.Message}");
            return "";
        }
    }

    private async Task<bool> IsSteamIDaTester(string steamId64)
    {
        try
        {
            var response = await httpClient.GetStringAsync(testerPersonalGifsSource);

            using (var jsonDocument = JsonDocument.Parse(response))
            {
                if (jsonDocument.RootElement.TryGetProperty(steamId64, out var isTester))
                    return true;
                return false;
            }
        }
        catch (Exception ex)
        {
            SharpTimerError($"Error in IsSteamIDaTester: {ex.Message}");
            return false;
        }
    }

    private void CheckPlayerCoords(CCSPlayerController? player, Vector playerSpeed)
    {
        try
        {
            if (player == null || !IsAllowedPlayer(player)) return;

            Vector incorrectVector = new(0, 0, 0);
            var playerPos = player.Pawn?.Value!.CBodyComponent?.SceneNode!.AbsOrigin;
            var isInsideStartBox = false;
            var isInsideEndBox = false;

            if (playerPos == null || currentMapStartC1 == incorrectVector || currentMapStartC2 == incorrectVector ||
                currentMapEndC1 == incorrectVector || currentMapEndC2 == incorrectVector)
                return;
            if (!useTriggersAndFakeZones)
            {
                isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);
            }

            var isInsideBonusStartBox = new bool[11];
            var isInsideBonusEndBox = new bool[11];
            foreach (var bonus in totalBonuses)
                if (bonus == 0)
                {
                }
                else
                {
                    if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                        currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                        currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                        currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                    {
                        SharpTimerError($"Invalid bonus coordinates for bonus {bonus}");
                    }
                    else
                    {
                        isInsideBonusStartBox[bonus] = IsVectorInsideBox(playerPos, currentBonusStartC1[bonus],
                            currentBonusStartC2[bonus]);
                        isInsideBonusEndBox[bonus] = IsVectorInsideBox(playerPos, currentBonusEndC1[bonus],
                            currentBonusEndC2[bonus]);
                    }
                }

            if (!useTriggersAndFakeZones)
            {
                if (!isInsideStartBox && isInsideEndBox)
                {
                    OnTimerStop(player);
                    if (enableReplays) OnRecordingStop(player);
                }
                else if (isInsideStartBox)
                {
                    if (playerTimers.TryGetValue(player.Slot, out var playerTimer))
                    {
                        playerTimer.inStartzone = true;
                        InvalidateTimer(player);
                    }
                }
                else if (!isInsideStartBox && playerTimers.TryGetValue(player.Slot, out var playerTimer))
                {
                    if (playerTimer.inStartzone)
                    {
                        OnTimerStart(player);
                        if (enableReplays) OnRecordingStart(player);

                        if ((maxStartingSpeedEnabled && use2DSpeed == false &&
                             Math.Round(playerSpeed.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled && use2DSpeed &&
                             Math.Round(playerSpeed.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity =
                                use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, true);
                        }
                    }

                    playerTimer.inStartzone = false;
                }
            }

            foreach (var bonus in totalBonuses)
                if (bonus == 0)
                {
                }
                else
                {
                    if (currentBonusStartC1 == null || currentBonusStartC1.Length <= bonus ||
                        currentBonusStartC2 == null || currentBonusStartC2.Length <= bonus ||
                        currentBonusEndC1 == null || currentBonusEndC1.Length <= bonus ||
                        currentBonusEndC2 == null || currentBonusEndC2.Length <= bonus)
                    {
                        SharpTimerError($"Invalid bonus coordinates for bonus {bonus}");
                    }
                    else
                    {
                        if (!isInsideBonusStartBox[bonus] && isInsideBonusEndBox[bonus])
                        {
                            OnBonusTimerStop(player, bonus);
                            if (enableReplays) OnRecordingStop(player);
                        }
                        else if (isInsideBonusStartBox[bonus])
                        {
                            if (playerTimers.TryGetValue(player.Slot, out var playerTimer))
                                playerTimer.inStartzone = true;

                            OnTimerStart(player, bonus);
                            if (enableReplays) OnRecordingStart(player, bonus);

                            if ((maxStartingSpeedEnabled && use2DSpeed == false &&
                                 Math.Round(playerSpeed.Length()) > maxBonusStartingSpeed) ||
                                (maxStartingSpeedEnabled && use2DSpeed &&
                                 Math.Round(playerSpeed.Length2D()) > maxBonusStartingSpeed))
                            {
                                Action<CCSPlayerController?, float, bool> adjustVelocity =
                                    use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                                adjustVelocity(player, maxBonusStartingSpeed, true);
                            }
                        }
                        else if (!isInsideBonusStartBox[bonus])
                        {
                            if (playerTimers.TryGetValue(player.Slot, out var playerTimer))
                                playerTimer.inStartzone = false;
                        }
                    }
                }
        }
        catch (Exception ex)
        {
            SharpTimerError($"Error in CheckPlayerCoords: {ex.Message}");
        }
    }

    public bool CommandCooldown(CCSPlayerController? player)
    {
        if (playerTimers[player!.Slot].TicksSinceLastCmd < cmdCooldown)
        {
            PrintToChat(player, Localizer["command_cooldown"]);
            return true;
        }

        return false;
    }

    public bool IsTimerBlocked(CCSPlayerController? player)
    {
        if (!playerTimers[player!.Slot].IsTimerBlocked)
        {
            PrintToChat(player, Localizer["stop_using_timer"]);
            return true;
        }

        return false;
    }

    public bool ReplayCheck(CCSPlayerController? player)
    {
        if (playerTimers[player!.Slot].IsReplaying)
        {
            PrintToChat(player, Localizer["end_your_replay"]);
            return true;
        }

        return false;
    }

    public bool CanCheckpoint(CCSPlayerController? player)
    {
        if (cpOnlyWhenTimerStopped && playerTimers[player!.Slot].IsTimerBlocked == false)
        {
            PrintToChat(player,
                Localizer["cant_use_checkpoint", currentMapName!.Contains("surf_") ? "loc" : "checkpoint"]);
            PlaySound(player, cpSoundError);
            return true;
        }

        return false;
    }
}