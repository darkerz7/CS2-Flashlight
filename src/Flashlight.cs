using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using static CounterStrikeSharp.API.Core.Listeners;

namespace Flashlight
{
	[MinimumApiVersion(330)]
	public class Flashlight : BasePlugin
	{
		public override string ModuleName => "Flashlight";
		public override string ModuleDescription => "Flashlight for Counter-Strike 2";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.2";

		static PlayerFlashlight[] g_PF = new PlayerFlashlight[65];
		static float g_fRainbowProgess = 0.0f;
		static CounterStrikeSharp.API.Modules.Timers.Timer? g_Timer = new(0.1f, OnTimerRainbow, TimerFlags.REPEAT);

		public override void Load(bool hotReload)
		{
			for (int i = 0; i < g_PF.Length; i++) g_PF[i] = new PlayerFlashlight();
			RegisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			RegisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
			RegisterListener<CheckTransmit>(OnCheckTransmit_Listener);
			RegisterListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);

			if (hotReload)
			{
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
				{
					g_PF[player.Slot].SetPlayer(player);
					if(player.Pawn?.Value?.LifeState == (byte)LifeState_t.LIFE_ALIVE) g_PF[player.Slot].CanToggle = true;
				});
			}
		}

		public override void Unload(bool hotReload)
		{
			if (g_Timer != null)
			{
				g_Timer.Kill();
				g_Timer = null;
			}
			RemoveListener<CheckTransmit>(OnCheckTransmit_Listener);
			RemoveListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);
			DeregisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			DeregisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);

			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
			{
				g_PF[player.Slot].RemoveFlashlight();
			});
		}

		private void OnOnPlayerButtonsChanged_Listener(CCSPlayerController player, PlayerButtons pressed, PlayerButtons released)
		{
			//Console.WriteLine($"[Debug:Buttons] Player: {player.PlayerName} Presed: {(long)pressed} Released: {(long)released}");
			if (player != null && player.IsValid)
			{
				if (g_PF[player.Slot].CanToggle && !g_PF[player.Slot].NotSpam && (pressed & (PlayerButtons)34359738368) != 0)
				{
					g_PF[player.Slot].NotSpam = true;
					if (g_PF[player.Slot].HasFlashlight()) g_PF[player.Slot].RemoveFlashlight();
					else g_PF[player.Slot].SpawnFlashlight();
					_ = new CounterStrikeSharp.API.Modules.Timers.Timer(0.5f, () => { g_PF[player.Slot].NotSpam = false; });
				}
			}
		}

		private static void OnTimerRainbow()
		{
			g_fRainbowProgess += 0.005f;
			if (g_fRainbowProgess >= 1.0f) g_fRainbowProgess = 0.0f;
			System.Drawing.Color RainBowColor = Rainbow(g_fRainbowProgess);
			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
			{
				if (g_PF[player.Slot].Rainbow)
				{
#nullable enable
					COmniLight? FL = g_PF[player.Slot].GetFlashLight();
#nullable disable
					if (FL != null)
					{
						FL.Color = RainBowColor;
						Utilities.SetStateChanged(FL, "CBarnLight", "m_Color");
					}
				}
			});
		}

		private void OnCheckTransmit_Listener(CCheckTransmitInfoList infoList)
		{
#nullable enable
			foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
#nullable disable
			{
				if (player == null || !player.IsValid) continue;
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
				{
					if(pl != player)
					{
#nullable enable
						COmniLight? FL = g_PF[pl.Slot].GetFlashLight();
#nullable disable
						if (FL != null) info.TransmitEntities.Remove(FL);
					}
				});
			}
		}

		[GameEventHandler]
		public HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_PF[player.Slot].SetPlayer(player);

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_PF[player.Slot].RemovePlayer();

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_PF[player.Slot].CanToggle = true;

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_PF[player.Slot].CanToggle = false;

			g_PF[player.Slot].RemoveFlashlight();

			return HookResult.Continue;
		}

		[ConsoleCommand("css_fl_color", "Allows the player to change the color of the flashlight")]
		[CommandHelper(minArgs: 3, usage: "[R G B] (default: 255 255 255; min 0; max 255)", whoCanExecute: CommandUsage.CLIENT_ONLY)]
#nullable enable
		public void OnChangeColor(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			if (!int.TryParse(command.GetArg(1), out int iRed)) iRed = 255;
			if (!int.TryParse(command.GetArg(2), out int iGreen)) iGreen = 255;
			if (!int.TryParse(command.GetArg(3), out int iBlue)) iBlue = 255;
			if (iRed >= 0 && iRed <= 255 && iGreen >= 0 && iGreen <= 255 && iBlue >= 0 && iBlue <= 255)
			{
				g_PF[player.Slot].ColorFL = System.Drawing.Color.FromArgb(iRed, iGreen, iBlue);
#nullable enable
				COmniLight? FL = g_PF[player.Slot].GetFlashLight();
#nullable disable
				if (FL != null)
				{
					FL.Color = g_PF[player.Slot].ColorFL;
					Utilities.SetStateChanged(FL, "CBarnLight", "m_Color");
				}
				command.ReplyToCommand($" {ChatColors.LightBlue}[{ChatColors.Green}Flashlight{ChatColors.LightBlue}] {ChatColors.White} You set the color {ChatColors.Red}{iRed} {ChatColors.Green}{iGreen} {ChatColors.Blue}{iBlue}");
			}
			else command.ReplyToCommand($" {ChatColors.LightBlue}[{ChatColors.Green}Flashlight{ChatColors.LightBlue}] {ChatColors.White}Bad color");
		}
		[ConsoleCommand("css_fl_rainbow", "Allows players to use a rainbow flashlight")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
#nullable enable
		public void OnChangeRainbow(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			if (g_PF[player.Slot].Rainbow)
			{
				g_PF[player.Slot].Rainbow = false;
#nullable enable
				COmniLight? FL = g_PF[player.Slot].GetFlashLight();
#nullable disable
				if (FL != null)
				{
					FL.Color = g_PF[player.Slot].ColorFL;
					Utilities.SetStateChanged(FL, "CBarnLight", "m_Color");
				}
				command.ReplyToCommand($" {ChatColors.LightBlue}[{ChatColors.Green}Flashlight{ChatColors.LightBlue}] {ChatColors.White} You {ChatColors.Red}Disabled {ChatColors.White}the {ChatColors.Red}r{ChatColors.Orange}a{ChatColors.Yellow}i{ChatColors.Green}n{ChatColors.LightBlue}b{ChatColors.Blue}o{ChatColors.Purple}w {ChatColors.White}flashlight");
			} else
			{
				g_PF[player.Slot].Rainbow = true;
				command.ReplyToCommand($" {ChatColors.LightBlue}[{ChatColors.Green}Flashlight{ChatColors.LightBlue}] {ChatColors.White} You {ChatColors.Green}Enabled {ChatColors.White}the {ChatColors.Red}r{ChatColors.Orange}a{ChatColors.Yellow}i{ChatColors.Green}n{ChatColors.LightBlue}b{ChatColors.Blue}o{ChatColors.Purple}w {ChatColors.White}flashlight");
			}
		}


		class PlayerFlashlight
		{
#nullable enable
			CCSPlayerController? Player = null;
			COmniLight? Flashlight_Ent = null;
#nullable disable
			public System.Drawing.Color ColorFL = System.Drawing.Color.White;
			public bool CanToggle = false;
			public bool NotSpam = false;
			public bool Rainbow = false;
#nullable enable
			public void SetPlayer(CCSPlayerController? player)
#nullable disable
			{
				CanToggle = false;
				NotSpam = false;
				Rainbow = false;

				RemoveFlashlight();
				Player = player;
			}
			public void RemovePlayer()
			{
				CanToggle = false;
				NotSpam = false;
				Rainbow = false;

				RemoveFlashlight();
				Player = null;
			}
			public void RemoveFlashlight()
			{
				if (Flashlight_Ent != null && Flashlight_Ent.IsValid) Flashlight_Ent.Remove();
				Flashlight_Ent = null;
			}
			public void SpawnFlashlight()
			{
				if (Player == null || !Player.IsValid) return;
				//RemoveFlashlight();
				var entity = Utilities.CreateEntityByName<COmniLight>("light_omni2");
				if (entity != null && entity.IsValid)
				{
					entity.DirectLight = 3;
					entity.OuterAngle = 45f;
					entity.Enabled = true;
					entity.Color = ColorFL;
					entity.ColorTemperature = 6500;
					entity.Brightness = 1f;
					entity.Range = 5000f;

					System.Numerics.Vector3 vecOrigin = (System.Numerics.Vector3)Player.PlayerPawn.Value!.AbsOrigin! with { Z = Player.PlayerPawn.Value!.AbsOrigin!.Z + Player.PlayerPawn.Value!.ViewOffset.Z + 0.03f };
					entity.Teleport(vecOrigin, (System.Numerics.Vector3)Player.PlayerPawn.Value!.EyeAngles, (System.Numerics.Vector3)Player.PlayerPawn.Value!.AbsVelocity);

					entity.DispatchSpawn();

					entity.AcceptInput("SetParent", Player.Pawn.Value, null, "!activator");
					entity.AcceptInput("SetParentAttachmentMaintainOffset", Player.Pawn.Value, null, "axis_of_intent");

					Flashlight_Ent = entity;
				}
			}
			public bool HasFlashlight()
			{
				if (Flashlight_Ent != null && Flashlight_Ent.IsValid) return true;
				return false;
			}
#nullable enable
			public COmniLight? GetFlashLight()
#nullable disable
			{
				if (HasFlashlight()) return Flashlight_Ent;
				return null;
			}
		}
		public static System.Drawing.Color Rainbow(float progress)
		{
			float div = (Math.Abs(progress % 1) * 6);
			int ascending = (int)((div % 1) * 255);
			int descending = 255 - ascending;

			return (int)div switch
			{
				0 => System.Drawing.Color.FromArgb(255, 255, ascending, 0),
				1 => System.Drawing.Color.FromArgb(255, descending, 255, 0),
				2 => System.Drawing.Color.FromArgb(255, 0, 255, ascending),
				3 => System.Drawing.Color.FromArgb(255, 0, descending, 255),
				4 => System.Drawing.Color.FromArgb(255, ascending, 0, 255),
				_ => System.Drawing.Color.FromArgb(255, 255, 0, descending),
			};
		}
	}
}