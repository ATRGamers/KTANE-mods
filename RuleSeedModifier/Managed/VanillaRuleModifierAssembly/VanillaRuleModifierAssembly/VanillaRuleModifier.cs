using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VanillaRuleModifierAssembly;
using VanillaRuleModifierAssembly.RuleSetGenerators;
using static VanillaRuleModifierAssembly.CommonReflectedTypeInfo;
using Settings = VanillaRuleModifierAssembly.ModSettings;

// ReSharper disable once CheckNamespace
public class VanillaRuleModifier : MonoBehaviour
{
    private KMGameInfo _gameInfo = null;
    public static Settings _modSettings;

    public int CurrentSeed
    {
        get
        {
            if (CurrentState != KMGameInfo.State.Setup && CurrentState != KMGameInfo.State.PostGame)
                return _currentSeed;
            return _modSettings.Settings.RuleSeed;
        }
    }

    private int _currentSeed;

    public bool CurrentRandomSeed
    {
        get
        {
            if (CurrentState != KMGameInfo.State.Setup && CurrentState != KMGameInfo.State.PostGame)
                return _currentRandomSeed;
            return _modSettings.Settings.RandomRuleSeed;
        }
    }

    private bool _currentRandomSeed;

    // Use this for initialization
    // ReSharper disable once UnusedMember.Local
    private static bool _fixesApplied = false;
    private static void ApplyBugFixes()
    {
        if (_fixesApplied) return;
        //DebugLog("No Fixes to apply. :)");
        _fixesApplied = true;
    }

    internal static RuleSeedModifierProperties[] PublicProperties = new RuleSeedModifierProperties[2];
    internal static HashSet<string> ModsThatSupportRuleSeedModifier = new HashSet<string>
    {
        "WireSetComponentSolver","ButtonComponentSolver",
        "WireSequenceComponentSolver","WhosOnFirstComponentSolver",
        "VennWireComponentSolver","SimonComponentSolver",
        "PasswordComponentSolver","NeedyKnobComponentSolver",
        "MorseCodeComponentSolver","MemoryComponentSolver",
        "KeypadComponentSolver","InvisibleWallsComponentSolver"
    };
    private bool _started = false;
    private void Start()
    {
        _started = true;
        ApplyBugFixes();
        //DestroyImmediate(GetComponent<KMService>()); //Hide from Mod Selector
        _modSettings = new Settings(GetComponent<KMModSettings>());

        if (!Initialize())
        {
            DebugLog("Failed to initialize the reflection component of Vanilla Rule Modifier. Aborting load");
            return;
        }
        if (!_modSettings.ReadSettings())
        {
            DebugLog("Failed to initialize Mod settings. Aborting load");
            return;
        }

        GameObject infoObject = new GameObject("VanillaRuleModifierProperties");
        infoObject.transform.parent = gameObject.transform;
        PublicProperties[0] = infoObject.AddComponent<RuleSeedModifierProperties>();
        PublicProperties[0].VanillaRuleModifer = this;

        GameObject infoObject2 = new GameObject("RuleSeedModifierProperties");
        infoObject2.transform.parent = gameObject.transform;
        PublicProperties[1] = infoObject2.AddComponent<RuleSeedModifierProperties>();
        PublicProperties[1].VanillaRuleModifer = this;

        foreach (string mod in ModsThatSupportRuleSeedModifier)
        {
            RuleSeedModifierProperties.AddSupportedModule(mod);
        }

        _gameInfo = GetComponent<KMGameInfo>();
        LoadMod();
    }

    public void SetRuleSeed(int seed, bool writeSettings)
    {
        if (seed == int.MinValue) seed = 0;
        _modSettings.Settings.RuleSeed = Mathf.Abs(seed);
        if (writeSettings) _modSettings.WriteSettings();
    }

    public void SetRandomRuleSeed(bool setting, bool writeSettings)
    {
        _modSettings.Settings.RandomRuleSeed = setting;
        if (writeSettings) _modSettings.WriteSettings();
    }

    public string GenerateManual()
    {
        if (CurrentState == KMGameInfo.State.Setup || CurrentState == KMGameInfo.State.PostGame)
            GenerateRules(_modSettings.Settings.RuleSeed);
        ManualGenerator.Instance.WriteManual(_modSettings.Settings.RuleSeed);
        return Path.Combine(Application.persistentDataPath, Path.Combine("ModifiedManuals", _modSettings.Settings.RuleSeed.ToString()));
    }

    private bool _enabled;
    // ReSharper disable once UnusedMember.Local
    private void OnDestroy()
    {
        DebugLog("Service prefab destroyed. Shutting down.");
        UnloadMod();
        _started = false;
    }

    private void OnEnable()
    {
        if (!_started || _enabled) return;
        DebugLog("Service prefab Enabled.");
        LoadMod();
    }

    private void OnDisable()
    {
        if (!_enabled) return;
        DebugLog("Service Prefab Disabled.");
        UnloadMod();
    }

    private void LoadMod()
    {
        _gameInfo.OnStateChange += OnStateChange;
        _enabled = true;
        CurrentState = KMGameInfo.State.Setup;
        _prevState = KMGameInfo.State.Setup;
    }

    private void UnloadMod()
    {
        UnloadRuleManager();
        _gameInfo.OnStateChange -= OnStateChange;
        _enabled = false;

        StopAllCoroutines();
    }

    public KMGameInfo.State CurrentState = KMGameInfo.State.Unlock;
    private KMGameInfo.State _prevState = KMGameInfo.State.Unlock;
    private Coroutine AddWidget;
    private Coroutine FixMorseCode;
    private void OnStateChange(KMGameInfo.State state)
    {
        if (AddWidget != null)
        {
            StopCoroutine(AddWidget);
            AddWidget = null;
        }

        if (FixMorseCode != null)
        {
            StopCoroutine(FixMorseCode);
            FixMorseCode = null;
        }

        //DebugLog("Transitioning from {1} to {0}", state, CurrentState);
        //if((_prevState == KMGameInfo.State.Setup || _prevState == KMGameInfo.State.PostGame) && CurrentState == KMGameInfo.State.Transitioning && state == KMGameInfo.State.Transitioning)
        if (CurrentState == KMGameInfo.State.Setup && state == KMGameInfo.State.Transitioning)
        {
            _modSettings.ReadSettings();
            var seed = _modSettings.Settings.RuleSeed;
            int minSeed = _modSettings.Settings.MinRandomSeed;
            int maxSeed = _modSettings.Settings.MaxRandomSeed

            if (_modSettings.Settings.RandomRuleSeed)
                if ( maxSeed < 0 ){
                    maxSeed = int.MaxValue;
                }

                if ( minSeed < 0 ){
                    minSeed = 0;
                }

                if ( minSeed < maxSeed ){
                    minSeed = maxSeed;
                }
                            
                seed = new System.Random().Next(
                    minSeed,
                    maxSeed
                );

            _currentSeed = seed;
            _currentRandomSeed = _modSettings.Settings.RandomRuleSeed;

            DebugLog("Generating Rules based on Seed {0}", seed);
            GenerateRules(seed);
            ManualGenerator.Instance.WriteManual(seed);
        }
        else if ((_prevState == KMGameInfo.State.Setup || _prevState == KMGameInfo.State.PostGame) && CurrentState == KMGameInfo.State.Transitioning && state == KMGameInfo.State.Transitioning)
        {
            if (_currentSeed != 1)
                AddWidget = StartCoroutine(AddWidgetToBomb(RuleSeedWidget));
        }
        else if (state == KMGameInfo.State.Gameplay)
        {
            FixMorseCode = StartCoroutine(FixMorseCodeModule());
        }

        _prevState = CurrentState;
        CurrentState = state;
    }

    public KMWidget RuleSeedWidget;

    public static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
        new Dictionary<string, object>
        {
            { "Filename", "VanillaRuleModifier-settings.txt" },
            { "Name", "Rule Seed Modifier" },
            { "Listings", new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "Key", "SettingsVersion" }, { "Text", "Settings Version" }, { "Description", "Don't touch this value. It is used by the mod internally to determine if\nthere are new settings to be saved." } },
                    new Dictionary<string, object> { { "Key", "ResetToDefault" }, { "Text", "Reset To Default" }, { "Description", "Changing this setting to true will reset ALL your setting back to default." } },
                    new Dictionary<string, object> { { "Key", "RuleSeed" }, { "Text", "Rule Seed" }, { "Description", "Sets the seed that will be used to generate the ruleset.\n1 = Vanilla." } },
                    new Dictionary<string, object> { { "Key", "RandomRuleSeed" }, { "Text", "Random Rule Seed" }, { "Description", "If enabled, then a random rule seed will be used each bomb." } },
                    new Dictionary<string, object> { { "Key", "MinRandomSeed" }, { "Text", "Min Random Seed" }, { "Description", "Set this value to however high you wish the seed to be." } },
                    new Dictionary<string, object> { { "Key", "MaxRandomSeed" }, { "Text", "Max Random Seed" }, { "Description", "Set this value to however high you wish the seed to be." } },
                    new Dictionary<string, object>
                    {
                        { "Key", "Language" },
                        { "Description", "Sets the language that manuals will be generated in.\nCurrently only \"en\" can be used." },
                        { "Type", "Dropdown" },
                        { "DropdownItems", new List<object> { "en" } }
                    },
                    new Dictionary<string, object> { { "Key", "ValidLanguages" }, { "Text", "Valid Languages" }, { "Description", "These are the languages that are currently supported." } },
                }
            }
        }
    };
}
