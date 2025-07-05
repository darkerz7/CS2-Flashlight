using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using static CounterStrikeSharp.API.Core.Listeners;

namespace Flashlight
{
	[MinimumApiVersion(285)]
	public class Flashlight : BasePlugin
	{
		public override string ModuleName => "Flashlight";
		public override string ModuleDescription => "Flashlight for Counter-Strike 2";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.1";

		static PlayerFlashlight[] g_PF = new PlayerFlashlight[65];
		static float g_fRainbowProgess = 0.0f;

		public override void Load(bool hotReload)
		{
			for (int i = 0; i < g_PF.Length; i++) g_PF[i] = new PlayerFlashlight();
			RegisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			RegisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
			RegisterListener<OnTick>(OnOnTick_Listener);
			RegisterListener<CheckTransmit>(OnCheckTransmit_Listener);

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
			RemoveListener<OnTick>(OnOnTick_Listener);
			RemoveListener<CheckTransmit>(OnCheckTransmit_Listener);
			DeregisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			DeregisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);

			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
			{
				g_PF[player.Slot].RemoveFlashlight();
			});
		}

		private void OnOnTick_Listener()
		{
			g_fRainbowProgess += 0.001f;
			if (g_fRainbowProgess >= 1.0f) g_fRainbowProgess = 0.0f;
			System.Drawing.Color RainBowColor = Rainbow(g_fRainbowProgess);
			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
			{
				if (g_PF[player.Slot].CanToggle && !g_PF[player.Slot].NotSpam && (player.Buttons & (PlayerButtons)34359738368) != 0)
				{
					g_PF[player.Slot].NotSpam = true;
					if (g_PF[player.Slot].HasFlashlight()) g_PF[player.Slot].RemoveFlashlight();
					else g_PF[player.Slot].SpawnFlashlight();
					_ = new CounterStrikeSharp.API.Modules.Timers.Timer(0.5f, () => { g_PF[player.Slot].NotSpam = false; });
				}
				g_PF[player.Slot].IsCrouch = (player.Buttons & PlayerButtons.Duck) != 0;
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
			foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
			{
				if (player == null || !player.IsValid) continue;
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
				{
					if(pl != player)
					{
						COmniLight? FL = g_PF[pl.Slot].GetFlashLight();
						if(FL != null) info.TransmitEntities.Remove(FL);
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

			g_PF[player.Slot].IsCrouch = false;
			g_PF[player.Slot].CanToggle = true;

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_PF[player.Slot].IsCrouch = false;
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
			CHandle<CCSGOViewModel>? ViewModel = null;
			COmniLight? Flashlight_Ent = null;
#nullable disable
			CounterStrikeSharp.API.Modules.Utils.Vector Origin = new();
			public System.Drawing.Color ColorFL = System.Drawing.Color.White;
			public bool IsCrouch = false;
			public bool CanToggle = false;
			public bool NotSpam = false;
			public bool Rainbow = false;
#nullable enable
			public void SetPlayer(CCSPlayerController? player)
#nullable disable
			{
				IsCrouch = false;
				CanToggle = false;
				NotSpam = false;
				Rainbow = false;

				RemoveFlashlight();
				Player = player;
				CreateViewModel();
			}
			public void RemovePlayer()
			{
				IsCrouch = false;
				CanToggle = false;
				NotSpam = false;
				Rainbow = false;

				RemoveFlashlight();
				ViewModel = null;
				Player = null;
			}
			void CreateViewModel()
			{
				if (Player == null || !Player.IsValid) return;
				CCSPlayerPawn pawn = Player.PlayerPawn.Value!;
				var handle = new CHandle<CCSGOViewModel>((IntPtr)(pawn.ViewModelServices!.Handle + Schema.GetSchemaOffset("CCSPlayer_ViewModelServices", "m_hViewModel") + 4));
				if (!handle.IsValid)
				{
					CCSGOViewModel viewmodel = Utilities.CreateEntityByName<CCSGOViewModel>("predicted_viewmodel")!;
					handle.Raw = viewmodel.EntityHandle.Raw;
					Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_pViewModelServices");
				}
				ViewModel = handle;
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

					Origin.X = Player.PlayerPawn.Value!.AbsOrigin!.X;
					Origin.Y = Player.PlayerPawn.Value!.AbsOrigin!.Y;
					Origin.Z = Player.PlayerPawn.Value!.AbsOrigin!.Z + (IsCrouch ? 46.03f : 64.03f);
					entity.Teleport(Origin, Player.PlayerPawn.Value!.EyeAngles, Player.PlayerPawn.Value!.AbsVelocity);

					entity.DispatchSpawn();

					if (ViewModel != null && ViewModel.IsValid)
						entity.AcceptInput("SetParent", ViewModel.Value, null, "!activator");
					else
						entity.AcceptInput("SetParent", Player.Pawn.Value, null, "!activator");

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

			switch ((int)div)
			{
				case 0:
					return System.Drawing.Color.FromArgb(255, 255, ascending, 0);
				case 1:
					return System.Drawing.Color.FromArgb(255, descending, 255, 0);
				case 2:
					return System.Drawing.Color.FromArgb(255, 0, 255, ascending);
				case 3:
					return System.Drawing.Color.FromArgb(255, 0, descending, 255);
				case 4:
					return System.Drawing.Color.FromArgb(255, ascending, 0, 255);
				default: // case 5:
					return System.Drawing.Color.FromArgb(255, 255, 0, descending);
			}
		}
	}
}