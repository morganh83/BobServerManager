using BobServerManager.Core;

namespace BobServerManager.UI;

/// <summary>
/// Editor for the scalar (non-array) Game.ini keys - the settings most admins actually tweak day
/// to day. Section/key names here are taken verbatim from the community "Beast of Bermuda Config
/// Tool" (cleafspear.github.io) generator, cross-checked against the official wiki. Anything not
/// covered here (creature limit arrays, world-event exclusion lists, mixed-herd groups, etc.) is
/// left completely untouched by this tab and can still be hand-edited on the Raw INI tab.
/// </summary>
public class GeneralSettingsPanel : UserControl, IConfigSubTab
{
    private const string Main = "/Script/BeastsOfBermuda.ServerGameInstance";
    private const string Skins = "Skins";
    private const string Weather = "/Script/BeastsOfBermuda.WeatherControllerBase";
    private const string SaveSystem = "/Script/BeastsOfBermuda.SaveSystem";
    private const string GameModeBase = "/Script/BeastsOfBermuda.BBGameModeBase";
    private const string Rcon = "/Script/BeastsOfBermuda.RCONHandler";
    private const string Reporter = "GameReporter";

    // --- Basic ---
    private readonly ComboBox _mapOverride = Combo();
    private readonly ComboBox _gameMode = Combo("Life_Cycle", "Combat", "Free_Roam");
    private readonly ComboBox _experience = Combo("PvPvE", "PvP", "PvE");
    private readonly ComboBox _foodDifficulty = Combo("Easy", "Normal", "Hard", "Expert");
    private readonly ComboBox _waterDifficulty = Combo("Easy", "Normal", "Hard", "Expert");
    private readonly ComboBox _waterDirtiness = Combo("Easy", "Normal", "Hard", "Expert");
    private readonly ComboBox _growthRate = Combo("VerySlow", "Slow", "Normal", "Fast", "VeryFast");
    private readonly ComboBox _foliageSpeed = Combo("VerySlow", "Slow", "Normal", "Fast", "VeryFast");
    private readonly NumericUpDown _maxTalents = NumUpDown(-1, 100, 0);
    private readonly NumericUpDown _adminSlots = NumUpDown(0, 50, 2);

    private readonly CheckBox _discordEnable = Check("Show Discord link on connect screen");
    private readonly TextBox _discordLink = TextBoxWide();
    private readonly CheckBox _steamGroupRequire = Check("Require Steam group membership to join");
    private readonly TextBox _steamGroupName = TextBoxWide();
    private readonly CheckBox _motd = Check("Show message of the day / intro message");
    private readonly CheckBox _disableRandomNames = Check("Disable random player names");
    private readonly CheckBox _consoleLocked = Check("Lock server console (disable in-game admin console)");
    private readonly CheckBox _disableGlobalChat = Check("Disable global chat");
    private readonly CheckBox _disableLocalChat = Check("Disable local chat");
    private readonly CheckBox _disableLeaderboard = Check("Disable leaderboard");

    // --- World / creatures ---
    private readonly NumericUpDown _carcassRate = NumFloat(0, 8, 1);
    private readonly NumericUpDown _carcassRatio = NumFloat(0, 2, 1);
    private readonly CheckBox _disableRestlessDebuff = Check("Disable restless (no-sleep) debuff");
    private readonly CheckBox _disableRandomEggs = Check("Disable random egg spawns");
    private readonly NumericUpDown _eggSpawnChance = NumFloat(0, 1, 2);
    private readonly CheckBox _disableAISpawning = Check("Disable AI creature spawning");
    private readonly NumericUpDown _aiSpawnRateMult = NumFloat(0, 10, 1);
    private readonly NumericUpDown _aiSpawnCapMult = NumFloat(0, 10, 1);
    private readonly CheckBox _spawnWorldEvents = Check("Enable world events");
    private readonly NumericUpDown _eventPrep = NumUpDown(0, 1440, 0);
    private readonly NumericUpDown _eventDuration = NumUpDown(0, 1440, 0);
    private readonly NumericUpDown _eventSpacing = NumUpDown(0, 10080, 0);
    private readonly CheckBox _disableRevengeKill = Check("Disable revenge-kill protection");
    private readonly NumericUpDown _dayLengthValue = NumUpDown(1, 999999, 0);
    private readonly ComboBox _dayLengthUnit = Combo("Seconds", "Minutes", "Hours", "Days");
    private readonly NumericUpDown _tunnelDespawnValue = NumUpDown(1, 999999, 0);
    private readonly ComboBox _tunnelDespawnUnit = Combo("Seconds", "Minutes", "Hours", "Days");
    private readonly CheckBox _disableResurrections = Check("Disable resurrections");
    private readonly CheckBox _disableReincarnations = Check("Disable reincarnations");
    private readonly CheckBox _portalsDisabled = Check("Disable map portals");
    private readonly CheckBox _forestFires = Check("Enable forest fires");
    private readonly CheckBox _mixedHerdCaps = Check("Use mixed-herd group caps");
    private readonly CheckBox _hardGroupLimits = Check("Use hard group limits");
    private readonly CheckBox _ignoreGroupSpeciesOnLogin = Check("Ignore group species validation on login");
    private readonly NumericUpDown _skinGrowthThreshold = NumFloat(0, 100, 1);

    // --- Weather ---
    private readonly NumericUpDown _oceanHeight = NumFloatSigned(-1000, 1000, 1);
    private readonly CheckBox _capStormSurge = Check("Cap storm surge height");
    private readonly CheckBox _stormsCauseStress = Check("Storms cause stress");
    private readonly CheckBox _spawnTornados = Check("Spawn tornados");
    private readonly NumericUpDown _weatherSpeed = NumFloat(0, 10, 1);
    private readonly NumericUpDown _weatherIntensity = NumFloat(0, 10, 1);
    private readonly NumericUpDown _stormSpacingMult = NumFloat(0, 10, 1);
    private readonly NumericUpDown _stormSurgeMult = NumFloat(0, 10, 1);
    private readonly NumericUpDown _rainCommonness = NumUpDown(0, 100, 0);
    private readonly NumericUpDown _fogCommonness = NumUpDown(0, 100, 0);
    private readonly NumericUpDown _overcastCommonness = NumUpDown(0, 100, 0);
    private readonly NumericUpDown _dryLightningCommonness = NumUpDown(0, 100, 0);

    // --- Save system ---
    private readonly CheckBox _disableCharacterDeath = Check("Disable permanent character death");
    private readonly NumericUpDown _autosaveTime = NumUpDown(30, 100000, 0);

    // --- RCON (native, beta per the devs) ---
    private readonly CheckBox _rconLiveMessages = Check("Route live chat/log messages to RCON output");
    private readonly CheckBox _rconEnable = Check("Enable RCON handler");
    private readonly TextBox _rconIp = new() { Width = 130, Text = "127.0.0.1" };
    private readonly NumericUpDown _rconPort = NumUpDown(1, 65535, 27015);

    // --- Discord activity reporter (native game feature; separate from this tool's own reboot-warning webhook) ---
    private readonly DataGridView _reporterGrid = new();
    private static readonly string[] ReporterCategories = { "Chat", "Combat", "Login", "AdminCmd", "Group", "Events" };

    public GeneralSettingsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BuildUi();
    }

    private void BuildUi()
    {
        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(10) };

        root.Controls.Add(GroupOf("Basic", Row("Map name override:", _mapOverride), Row("Game mode:", _gameMode),
            Row("Preferred experience:", _experience), Row("Food drain difficulty:", _foodDifficulty),
            Row("Water drain difficulty:", _waterDifficulty), Row("Water dirtiness difficulty:", _waterDirtiness),
            Row("Global growth rate:", _growthRate), Row("Foliage spawn rate:", _foliageSpeed),
            Row("Max talents allowed (-1 = unlimited):", _maxTalents), Row("Reserved admin slots:", _adminSlots),
            Row("Discord link:", _discordLink), Row("Steam group name:", _steamGroupName),
            Bools(_discordEnable, _steamGroupRequire, _motd, _disableRandomNames, _consoleLocked,
                  _disableGlobalChat, _disableLocalChat, _disableLeaderboard)));

        root.Controls.Add(GroupOf("World & Creatures",
            Row("Carcass spawn rate multiplier:", _carcassRate), Row("Carcass spawn ratio:", _carcassRatio),
            Row("Random egg spawn chance:", _eggSpawnChance), Row("AI spawn rate multiplier:", _aiSpawnRateMult),
            Row("AI spawn cap multiplier:", _aiSpawnCapMult),
            Row("World event prep time (min):", _eventPrep), Row("World event duration (min):", _eventDuration),
            Row("World event spacing (min):", _eventSpacing),
            UnitRow("Day length:", _dayLengthValue, _dayLengthUnit),
            UnitRow("Tunnel despawn time:", _tunnelDespawnValue, _tunnelDespawnUnit),
            Row("Skin growth threshold:", _skinGrowthThreshold),
            Bools(_disableRestlessDebuff, _disableRandomEggs, _disableAISpawning, _spawnWorldEvents,
                  _disableRevengeKill, _disableResurrections, _disableReincarnations, _portalsDisabled,
                  _forestFires, _mixedHerdCaps, _hardGroupLimits, _ignoreGroupSpeciesOnLogin)));

        root.Controls.Add(GroupOf("Weather",
            Row("Ocean height adjustment:", _oceanHeight), Row("Weather speed modifier:", _weatherSpeed),
            Row("Weather intensity modifier:", _weatherIntensity), Row("Storm spacing multiplier:", _stormSpacingMult),
            Row("Storm surge multiplier:", _stormSurgeMult), Row("Rain commonness:", _rainCommonness),
            Row("Fog commonness:", _fogCommonness), Row("Overcast commonness:", _overcastCommonness),
            Row("Dry lightning commonness:", _dryLightningCommonness),
            Bools(_capStormSurge, _stormsCauseStress, _spawnTornados)));

        root.Controls.Add(GroupOf("Save System",
            Row("Autosave interval (seconds, min 30):", _autosaveTime),
            Bools(_disableCharacterDeath)));

        root.Controls.Add(GroupOf("RCON (native, marked Beta by the developers)",
            Row("RCON bind IP:", _rconIp), Row("RCON port:", _rconPort),
            Bools(_rconEnable, _rconLiveMessages)));

        _reporterGrid.Width = 700;
        _reporterGrid.Height = 190;
        _reporterGrid.AllowUserToAddRows = false;
        _reporterGrid.AllowUserToDeleteRows = false;
        _reporterGrid.RowHeadersVisible = false;
        _reporterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", ReadOnly = true, Width = 90 });
        _reporterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled", Width = 60 });
        _reporterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Webhook", HeaderText = "Discord Webhook URL", Width = 300 });
        _reporterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Icon", HeaderText = "Icon URL", Width = 220 });
        foreach (var cat in ReporterCategories)
            _reporterGrid.Rows.Add(cat, false, "", "");

        root.Controls.Add(GroupOf("Discord Activity Reporter (native game feature - separate from this tool's reboot-warning webhook)", _reporterGrid));

        Controls.Add(root);
    }

    public void RefreshFromDocument(IniDocument doc)
    {
        _mapOverride.Text = doc.GetValue(Main, "MapNameOverride") ?? "";
        _gameMode.Text = doc.GetValue(Main, "GameMode") ?? "Life_Cycle";
        _experience.Text = doc.GetValue(Main, "PreferredGameExperience") ?? "";
        _foodDifficulty.Text = doc.GetValue(Main, "FoodDrainDifficulty") ?? "";
        _waterDifficulty.Text = doc.GetValue(Main, "WaterDrainDifficulty") ?? "";
        _waterDirtiness.Text = doc.GetValue(Main, "WaterDirtinessDifficulty") ?? "";
        _growthRate.Text = doc.GetValue(Main, "GrowthRate") ?? "";
        _foliageSpeed.Text = doc.GetValue(Main, "FoliageSpawnSpeed") ?? "";
        _maxTalents.Value = Clamp(doc.GetInt(Main, "MaxTalentsAllowed", -1), _maxTalents);
        _adminSlots.Value = Clamp(doc.GetInt(Main, "ReservedAdminSlots", 2), _adminSlots);

        _discordEnable.Checked = doc.GetBool(Main, "bDisplayDiscordLink", false);
        _discordLink.Text = doc.GetValue(Main, "DiscordLink") ?? "";
        _steamGroupRequire.Checked = doc.GetBool(Main, "bRequireSteamGroupToJoin", false);
        _steamGroupName.Text = doc.GetValue(Main, "SteamGroupName") ?? "";
        _motd.Checked = doc.GetBool(Main, "bDisplayIntroMessage", true);
        _disableRandomNames.Checked = doc.GetBool(Main, "bDisableUseRandomName", false);
        _consoleLocked.Checked = doc.GetBool(Main, "bConsoleLocked", false);
        _disableGlobalChat.Checked = doc.GetBool(Main, "bDisableGlobalChat", false);
        _disableLocalChat.Checked = doc.GetBool(Main, "bDisableLocalChat", false);
        _disableLeaderboard.Checked = doc.GetBool(Main, "bDisableLeaderboard", false);

        _carcassRate.Value = Clamp((decimal)doc.GetFloat(Main, "CarcassRateMultiplier", 1f), _carcassRate);
        _carcassRatio.Value = Clamp((decimal)doc.GetFloat(Main, "CarcassSpawnRatio", 1f), _carcassRatio);
        _disableRestlessDebuff.Checked = doc.GetBool(Main, "bDisableRestlessDebuff", false);
        _disableRandomEggs.Checked = doc.GetBool(Main, "bDisableRandomEggSpawns", false);
        _eggSpawnChance.Value = Clamp((decimal)doc.GetFloat(Main, "RandomEggSpawnChance", 0.1f), _eggSpawnChance);
        _disableAISpawning.Checked = doc.GetBool(Main, "bDisableAISpawning", false);
        _aiSpawnRateMult.Value = Clamp((decimal)doc.GetFloat(Main, "AISpawnRateMultiplier", 1f), _aiSpawnRateMult);
        _aiSpawnCapMult.Value = Clamp((decimal)doc.GetFloat(Main, "AISpawnCapMultiplier", 1f), _aiSpawnCapMult);
        _spawnWorldEvents.Checked = doc.GetBool(Main, "bSpawnWorldEvents", true);
        _eventPrep.Value = Clamp(doc.GetInt(Main, "WorldEventPrepTime", 0), _eventPrep);
        _eventDuration.Value = Clamp(doc.GetInt(Main, "WorldEventDuration", 0), _eventDuration);
        _eventSpacing.Value = Clamp(doc.GetInt(Main, "WorldEventSpacing", 0), _eventSpacing);
        _disableRevengeKill.Checked = doc.GetBool(Main, "bDisableRevengeKillProtection", false);

        SetUnitValue(doc.GetFloat(Main, "DayLength", 3600f), _dayLengthValue, _dayLengthUnit);
        SetUnitValue(doc.GetInt(Main, "TunnelNetworkDespawnTime", 3600), _tunnelDespawnValue, _tunnelDespawnUnit);

        _disableResurrections.Checked = doc.GetBool(Main, "bDisableResurrections", false);
        _disableReincarnations.Checked = doc.GetBool(Main, "bDisableReincarnations", false);
        _portalsDisabled.Checked = doc.GetBool(Main, "bPortalsDisabled", false);
        _forestFires.Checked = doc.GetBool(Main, "bSpawnForestFires", false);
        _mixedHerdCaps.Checked = doc.GetBool(Main, "bUseMixedHerdCaps", false);
        _hardGroupLimits.Checked = doc.GetBool(Main, "bUseHardGroupLimits", false);
        _ignoreGroupSpeciesOnLogin.Checked = doc.GetBool(Main, "bForceIgnoreGroupSpeciesCheckOnLogin", false);
        _skinGrowthThreshold.Value = Clamp((decimal)doc.GetFloat(Skins, "SkinLockGrowthThreshold", 0f), _skinGrowthThreshold);

        _oceanHeight.Value = Clamp((decimal)doc.GetFloat(Weather, "OceanHeightAdd", 0f), _oceanHeight);
        _capStormSurge.Checked = doc.GetBool(Weather, "bCapStormSurge", false);
        _stormsCauseStress.Checked = doc.GetBool(Weather, "bStormsCauseStress", false);
        _spawnTornados.Checked = doc.GetBool(Weather, "bSpawnsTornados", false);
        _weatherSpeed.Value = Clamp((decimal)doc.GetFloat(Weather, "SpeedModifier", 1f), _weatherSpeed);
        _weatherIntensity.Value = Clamp((decimal)doc.GetFloat(Weather, "IntensityModifier", 1f), _weatherIntensity);
        _stormSpacingMult.Value = Clamp((decimal)doc.GetFloat(Weather, "StormSpacingMultiplier", 1f), _stormSpacingMult);
        _stormSurgeMult.Value = Clamp((decimal)doc.GetFloat(Weather, "StormSurgeMultiplier", 1f), _stormSurgeMult);
        _rainCommonness.Value = Clamp(doc.GetInt(Weather, "RainCommonness", 1), _rainCommonness);
        _fogCommonness.Value = Clamp(doc.GetInt(Weather, "FogCommonness", 1), _fogCommonness);
        _overcastCommonness.Value = Clamp(doc.GetInt(Weather, "OvercastCommonness", 1), _overcastCommonness);
        _dryLightningCommonness.Value = Clamp(doc.GetInt(Weather, "DryLightningCommonness", 1), _dryLightningCommonness);

        _disableCharacterDeath.Checked = doc.GetBool(SaveSystem, "bDisableCharacterDeath", false);
        _autosaveTime.Value = Clamp(doc.GetInt(SaveSystem, "AutosaveTime", 120), _autosaveTime);

        _rconLiveMessages.Checked = doc.GetBool(GameModeBase, "bLiveMessagesToRCON", false);
        var rconPortVal = doc.GetValue(Rcon, "CommunicationPort");
        _rconEnable.Checked = rconPortVal != null;
        _rconIp.Text = doc.GetValue(Rcon, "IP4Binding") ?? "127.0.0.1";
        _rconPort.Value = Clamp(doc.GetInt(Rcon, "CommunicationPort", 27015), _rconPort);

        var webhookKeys = new (string enableKey, string urlKey, string iconKey)[]
        {
            ("bUseChatWebhook", "ChatReportDiscordWebhook", "ChatReportIconURL"),
            ("bUseCombatActivityWebhook", "CombatActivityDiscordWebhook", "CombatActivityDiscordIconURL"),
            ("bUseLoginReportWebhook", "LoginDiscordWebhook", "LoginDiscordIconURL"),
            ("bUseAdminCommandUsageWebhook", "AdminCmdDiscordWebhook", "AdminCmdDiscordIcon"),
            ("bUseGroupActivityWebhook", "GroupActivityDiscordWebhook", "GroupActivityDiscordIconURL"),
            ("bUseEventsWebhook", "EventsDiscordWebhook", "EventsDiscordIconURL"),
        };
        for (int i = 0; i < ReporterCategories.Length; i++)
        {
            var (enableKey, urlKey, iconKey) = webhookKeys[i];
            _reporterGrid.Rows[i].Cells["Enabled"].Value = doc.GetBool(Reporter, enableKey, false);
            _reporterGrid.Rows[i].Cells["Webhook"].Value = doc.GetValue(Reporter, urlKey) ?? "";
            _reporterGrid.Rows[i].Cells["Icon"].Value = doc.GetValue(Reporter, iconKey) ?? "";
        }
    }

    public void ApplyToDocument(IniDocument doc)
    {
        SetOrClear(doc, Main, "MapNameOverride", _mapOverride.Text, quoted: true);
        doc.SetValue(Main, "GameMode", _gameMode.Text);
        SetOrClear(doc, Main, "PreferredGameExperience", _experience.Text);
        SetOrClear(doc, Main, "FoodDrainDifficulty", _foodDifficulty.Text);
        SetOrClear(doc, Main, "WaterDrainDifficulty", _waterDifficulty.Text);
        SetOrClear(doc, Main, "WaterDirtinessDifficulty", _waterDirtiness.Text);
        SetOrClear(doc, Main, "GrowthRate", _growthRate.Text);
        SetOrClear(doc, Main, "FoliageSpawnSpeed", _foliageSpeed.Text);
        doc.SetInt(Main, "MaxTalentsAllowed", (int)_maxTalents.Value);
        doc.SetInt(Main, "ReservedAdminSlots", (int)_adminSlots.Value);

        doc.SetBool(Main, "bDisplayDiscordLink", _discordEnable.Checked);
        doc.SetValue(Main, "DiscordLink", _discordLink.Text, quoted: true);
        doc.SetBool(Main, "bRequireSteamGroupToJoin", _steamGroupRequire.Checked);
        doc.SetValue(Main, "SteamGroupName", _steamGroupName.Text, quoted: true);
        doc.SetBool(Main, "bDisplayIntroMessage", _motd.Checked);
        doc.SetBool(Main, "bDisableUseRandomName", _disableRandomNames.Checked);
        doc.SetBool(Main, "bConsoleLocked", _consoleLocked.Checked);
        doc.SetBool(Main, "bDisableGlobalChat", _disableGlobalChat.Checked);
        doc.SetBool(Main, "bDisableLocalChat", _disableLocalChat.Checked);
        doc.SetBool(Main, "bDisableLeaderboard", _disableLeaderboard.Checked);

        doc.SetFloat(Main, "CarcassRateMultiplier", (float)_carcassRate.Value);
        doc.SetFloat(Main, "CarcassSpawnRatio", (float)_carcassRatio.Value);
        doc.SetBool(Main, "bDisableRestlessDebuff", _disableRestlessDebuff.Checked);
        doc.SetBool(Main, "bDisableRandomEggSpawns", _disableRandomEggs.Checked);
        doc.SetFloat(Main, "RandomEggSpawnChance", (float)_eggSpawnChance.Value);
        doc.SetBool(Main, "bDisableAISpawning", _disableAISpawning.Checked);
        doc.SetFloat(Main, "AISpawnRateMultiplier", (float)_aiSpawnRateMult.Value);
        doc.SetFloat(Main, "AISpawnCapMultiplier", (float)_aiSpawnCapMult.Value);
        doc.SetBool(Main, "bSpawnWorldEvents", _spawnWorldEvents.Checked);
        doc.SetInt(Main, "WorldEventPrepTime", (int)_eventPrep.Value);
        doc.SetInt(Main, "WorldEventDuration", (int)_eventDuration.Value);
        doc.SetInt(Main, "WorldEventSpacing", (int)_eventSpacing.Value);
        doc.SetBool(Main, "bDisableRevengeKillProtection", _disableRevengeKill.Checked);

        doc.SetFloat(Main, "DayLength", GetUnitSeconds(_dayLengthValue, _dayLengthUnit));
        doc.SetInt(Main, "TunnelNetworkDespawnTime", (int)GetUnitSeconds(_tunnelDespawnValue, _tunnelDespawnUnit));

        doc.SetBool(Main, "bDisableResurrections", _disableResurrections.Checked);
        doc.SetBool(Main, "bDisableReincarnations", _disableReincarnations.Checked);
        doc.SetBool(Main, "bPortalsDisabled", _portalsDisabled.Checked);
        doc.SetBool(Main, "bSpawnForestFires", _forestFires.Checked);
        doc.SetBool(Main, "bUseMixedHerdCaps", _mixedHerdCaps.Checked);
        doc.SetBool(Main, "bUseHardGroupLimits", _hardGroupLimits.Checked);
        doc.SetBool(Main, "bForceIgnoreGroupSpeciesCheckOnLogin", _ignoreGroupSpeciesOnLogin.Checked);
        doc.SetFloat(Skins, "SkinLockGrowthThreshold", (float)_skinGrowthThreshold.Value);

        doc.SetFloat(Weather, "OceanHeightAdd", (float)_oceanHeight.Value);
        doc.SetBool(Weather, "bCapStormSurge", _capStormSurge.Checked);
        doc.SetBool(Weather, "bStormsCauseStress", _stormsCauseStress.Checked);
        doc.SetBool(Weather, "bSpawnsTornados", _spawnTornados.Checked);
        doc.SetFloat(Weather, "SpeedModifier", (float)_weatherSpeed.Value);
        doc.SetFloat(Weather, "IntensityModifier", (float)_weatherIntensity.Value);
        doc.SetFloat(Weather, "StormSpacingMultiplier", (float)_stormSpacingMult.Value);
        doc.SetFloat(Weather, "StormSurgeMultiplier", (float)_stormSurgeMult.Value);
        doc.SetInt(Weather, "RainCommonness", (int)_rainCommonness.Value);
        doc.SetInt(Weather, "FogCommonness", (int)_fogCommonness.Value);
        doc.SetInt(Weather, "OvercastCommonness", (int)_overcastCommonness.Value);
        doc.SetInt(Weather, "DryLightningCommonness", (int)_dryLightningCommonness.Value);

        doc.SetBool(SaveSystem, "bDisableCharacterDeath", _disableCharacterDeath.Checked);
        doc.SetInt(SaveSystem, "AutosaveTime", (int)_autosaveTime.Value);

        doc.SetBool(GameModeBase, "bLiveMessagesToRCON", _rconLiveMessages.Checked);
        if (_rconEnable.Checked)
        {
            doc.SetInt(Rcon, "CommunicationPort", (int)_rconPort.Value);
            doc.SetValue(Rcon, "IP4Binding", _rconIp.Text, quoted: true);
        }

        var webhookKeys = new (string enableKey, string urlKey, string iconKey)[]
        {
            ("bUseChatWebhook", "ChatReportDiscordWebhook", "ChatReportIconURL"),
            ("bUseCombatActivityWebhook", "CombatActivityDiscordWebhook", "CombatActivityDiscordIconURL"),
            ("bUseLoginReportWebhook", "LoginDiscordWebhook", "LoginDiscordIconURL"),
            ("bUseAdminCommandUsageWebhook", "AdminCmdDiscordWebhook", "AdminCmdDiscordIcon"),
            ("bUseGroupActivityWebhook", "GroupActivityDiscordWebhook", "GroupActivityDiscordIconURL"),
            ("bUseEventsWebhook", "EventsDiscordWebhook", "EventsDiscordIconURL"),
        };
        for (int i = 0; i < ReporterCategories.Length; i++)
        {
            var (enableKey, urlKey, iconKey) = webhookKeys[i];
            var enabled = _reporterGrid.Rows[i].Cells["Enabled"].Value is bool b && b;
            var url = _reporterGrid.Rows[i].Cells["Webhook"].Value?.ToString() ?? "";
            var icon = _reporterGrid.Rows[i].Cells["Icon"].Value?.ToString() ?? "";
            doc.SetBool(Reporter, enableKey, enabled);
            doc.SetValue(Reporter, urlKey, url, quoted: true);
            doc.SetValue(Reporter, iconKey, icon, quoted: true);
        }
    }

    private static void SetOrClear(IniDocument doc, string section, string key, string value, bool quoted = false)
        => doc.SetValue(section, key, value, quoted);

    private static void SetUnitValue(float seconds, NumericUpDown valueCtrl, ComboBox unitCombo)
    {
        (double divisor, string unit) = seconds switch
        {
            _ when seconds % 86400 == 0 && seconds >= 86400 => (86400d, "Days"),
            _ when seconds % 3600 == 0 && seconds >= 3600 => (3600d, "Hours"),
            _ when seconds % 60 == 0 && seconds >= 60 => (60d, "Minutes"),
            _ => (1d, "Seconds")
        };
        valueCtrl.Value = Clamp((decimal)(seconds / divisor), valueCtrl);
        unitCombo.Text = unit;
    }

    private static float GetUnitSeconds(NumericUpDown valueCtrl, ComboBox unitCombo)
    {
        double mult = unitCombo.Text switch
        {
            "Minutes" => 60,
            "Hours" => 3600,
            "Days" => 86400,
            _ => 1
        };
        return (float)((double)valueCtrl.Value * mult);
    }

    private static decimal Clamp(decimal v, NumericUpDown c) => Math.Min(Math.Max(v, c.Minimum), c.Maximum);
    private static decimal Clamp(int v, NumericUpDown c) => Clamp((decimal)v, c);

    // --- small UI factory helpers to keep the field declarations above readable ---
    private static ComboBox Combo(params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 200 };
        c.Items.AddRange(items);
        return c;
    }

    private static CheckBox Check(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 4, 16, 4) };

    private static NumericUpDown NumUpDown(int min, int max, int value) =>
        new() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 100 };

    private static NumericUpDown NumFloat(int min, int max, int decimals) =>
        new() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = 0.1M, Width = 100 };

    private static NumericUpDown NumFloatSigned(int min, int max, int decimals) =>
        new() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = 0.5M, Width = 100 };

    private static TextBox TextBoxWide() => new() { Width = 320 };

    private static Control Row(string label, Control control)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.Add(new Label { Text = label, AutoSize = true, Width = 230, Margin = new Padding(0, 6, 6, 0) });
        p.Controls.Add(control);
        return p;
    }

    private static Control UnitRow(string label, NumericUpDown value, ComboBox unit)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.Add(new Label { Text = label, AutoSize = true, Width = 230, Margin = new Padding(0, 6, 6, 0) });
        p.Controls.Add(value);
        p.Controls.Add(unit);
        return p;
    }

    private static Control Bools(params CheckBox[] boxes)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(760, 0), Margin = new Padding(0, 6, 0, 0) };
        p.Controls.AddRange(boxes);
        return p;
    }

    private static Control GroupOf(string title, params Control[] children)
    {
        var g = new GroupBox { Text = title, AutoSize = true, Padding = new Padding(10), Width = 760 };
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        stack.Controls.AddRange(children);
        g.Controls.Add(stack);
        return g;
    }
}
