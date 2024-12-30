using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using static CounterStrikeSharp.API.Core.Listeners;

namespace Flashlight
{
	[MinimumApiVersion(285)]
	public class Flashlight : BasePlugin
	{
		public override string ModuleName => "Flashlight";
		public override string ModuleDescription => "Flashlight for Counter-Strike 2";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.0";

		static bool[] g_bIsCrouch = new bool[65];
		static bool[] g_bCanToggle = new bool[65];
		static bool[] g_bNotSpam = new bool[65];
		Dictionary<CCSPlayerController, COmniLight?> g_Flashlight = new();

		public override void Load(bool hotReload)
		{
			RegisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			RegisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
			RegisterListener<OnTick>(OnOnTick_Listener);
			RegisterListener<CheckTransmit>(OnCheckTransmit_Listener);
		}

		public override void Unload(bool hotReload)
		{
			RemoveListener<OnTick>(OnOnTick_Listener);
			RemoveListener<CheckTransmit>(OnCheckTransmit_Listener);
			DeregisterEventHandler<EventPlayerDeath>(OnEventPlayerDeath);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			DeregisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
		}

		private void OnOnTick_Listener()
		{
			Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
			{
				TeleportFlashlight(player);
				if (g_bCanToggle[player.Slot] && !g_bNotSpam[player.Slot] && (player.Buttons & (PlayerButtons)34359738368) != 0)
				{
					g_bNotSpam[player.Slot] = true;
					if (g_Flashlight.TryGetValue(player, out var value) && value != null) RemoveFlashlight(player);
					else SpawnFlashlight(player);
					new CounterStrikeSharp.API.Modules.Timers.Timer(0.5f, () => { g_bNotSpam[player.Slot] = false; });
				}
				if ((player.Buttons & PlayerButtons.Duck) != 0) g_bIsCrouch[player.Slot] = true;
				else g_bIsCrouch[player.Slot] = false;
			});
		}

		private void OnCheckTransmit_Listener(CCheckTransmitInfoList infoList)
		{
			foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
			{
				if (player == null || !player.IsValid) continue;
				foreach (KeyValuePair<CCSPlayerController, COmniLight?> pair in g_Flashlight)
				{
					if (pair.Value != null && pair.Value.IsValid && pair.Key != player)
					{
						info.TransmitEntities.Remove(pair.Value);
					}
				}
			}
		}

		[GameEventHandler]
		public HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			var player = @event.Userid;

			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			if (!g_Flashlight.TryAdd(player, null) && g_Flashlight.TryGetValue(player, out var value) && value != null) RemoveFlashlight(player);

			g_bIsCrouch[player.Slot] = false;
			g_bCanToggle[player.Slot] = false;
			g_bNotSpam[player.Slot] = false;

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			var player = @event.Userid;

			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_bIsCrouch[player.Slot] = false;
			g_bCanToggle[player.Slot] = false;
			g_bNotSpam[player.Slot] = false;

			RemoveFlashlight(player);
			g_Flashlight.Remove(player);

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
		{
			var player = @event.Userid;

			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_bIsCrouch[player.Slot] = false;
			g_bCanToggle[player.Slot] = true;

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnEventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
		{
			var player = @event.Userid;

			if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

			g_bIsCrouch[player.Slot] = false;
			g_bCanToggle[player.Slot] = false;

			RemoveFlashlight(player);

			return HookResult.Continue;
		}

		public void RemoveFlashlight(CCSPlayerController player)
		{
			if (g_Flashlight.TryGetValue(player, out var value) && value != null && value.IsValid) value.Remove();

			g_Flashlight[player] = null;
		}

		public void SpawnFlashlight(CCSPlayerController player)
		{
			RemoveFlashlight(player);
			if (g_Flashlight.TryGetValue(player, out var value))
			{
				var entity = Utilities.CreateEntityByName<COmniLight>("light_omni2");
				if (LightTeleport(player, entity))
				{

					entity!.DirectLight = 3;
					entity.OuterAngle = 45f;
					entity.Enabled = true;
					entity.Color = System.Drawing.Color.White;
					entity.ColorTemperature = 6500;
					entity.Brightness = 1f;
					entity.Range = 5000f;

					entity.DispatchSpawn();
					g_Flashlight[player] = entity;
				}
			}
		}

		public void TeleportFlashlight(CCSPlayerController player)
		{
			g_Flashlight.TryGetValue(player, out var value);
			LightTeleport(player, value);
		}

		public bool LightTeleport(CCSPlayerController player, COmniLight? entity)
		{
			if (g_Flashlight.TryGetValue(player, out var value) && entity != null && entity.IsValid)
			{
				entity.Teleport(
					new CounterStrikeSharp.API.Modules.Utils.Vector(
						player.PlayerPawn.Value!.AbsOrigin!.X,
						player.PlayerPawn.Value!.AbsOrigin!.Y,
						player.PlayerPawn.Value!.AbsOrigin!.Z + (g_bIsCrouch[player.Slot] ? 46.03f : 64.03f)),
				player.PlayerPawn.Value!.EyeAngles,
				player.PlayerPawn.Value!.AbsVelocity
				);
				return true;
			}
			return false;
		}
	}
}