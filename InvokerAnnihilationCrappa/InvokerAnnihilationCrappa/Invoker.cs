using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.SDK.Service;
using Ensage.SDK.Service.Metadata;
using Ensage.SDK.TargetSelector;
using log4net;
using PlaySharp.Toolkit.Logging;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Ensage.SDK.Input;
using Ensage.SDK.Orbwalker;
using System.Windows.Input;
using Ensage.Common.Threading;
using Ensage.SDK.Abilities;
using Ensage.SDK.Abilities.Items;
using Ensage.SDK.Inventory;
using Ensage.SDK.Inventory.Metadata;
using Ensage.SDK.Prediction;
using Ensage.SDK.Renderer;
using Ensage.SDK.Renderer.Particle;

namespace InvokerAnnihilationCrappa
{
    [ExportPlugin("Invoker Crappahilation", author:"JumpAttacker", units: HeroId.npc_dota_hero_invoker)]
    public class Invoker : Plugin
    {
        private readonly Lazy<AbilityFactory> _abilityFactory;
        public Lazy<IServiceContext> Context { get; }
        public Lazy<IInventoryManager> InventoryManager { get; }
        public Lazy<IInputManager> Input { get; }
        public Lazy<IOrbwalkerManager> OrbwalkerManager { get; }
        public Lazy<ITargetSelectorManager> TargetManager { get; }
        public Lazy<IPrediction> Prediction { get; }
        public Lazy<IRendererManager> Renderer { get; }
        public Lazy<IParticleManager> ParticleManager { get; }
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Config Config { get; set; }
        public Unit Owner { get; }
        public int SelectedCombo;
        public readonly InvokerMode _mode;
        private Dictionary<Unit, IServiceContext> Orbwalkers { get; } = new Dictionary<Unit, IServiceContext>();

        [ImportingConstructor]
        public Invoker(
            [Import] Lazy<IServiceContext> context,
            [Import] Lazy<IInventoryManager> inventoryManager,
            [Import] Lazy<IInputManager> input,
            [Import] Lazy<IOrbwalkerManager> orbwalkerManager,
            [Import] Lazy<ITargetSelectorManager> targetManager,
            [Import] Lazy<IPrediction> prediction, [Import] Lazy<IRendererManager> renderer,
            [Import] Lazy<IParticleManager> particleManager, [Import] Lazy<AbilityFactory> abilityFactory)
        {
            _abilityFactory = abilityFactory;
            Context = context;
            InventoryManager = inventoryManager;
            Input = input;
            OrbwalkerManager = orbwalkerManager;
            TargetManager = targetManager;
            Prediction = prediction;
            Renderer = renderer;
            ParticleManager = particleManager;
            Owner = context.Value.Owner;
            /*_mode = new InvokerMode(
                Key.G,
                OrbwalkerManager,
                Input,
                InventoryManager,
                TargetManager,
                Prediction,
                renderer,
                particleManager);*/
            _mode = new InvokerMode(
                Key.G,
                OrbwalkerManager,
                Input,
                this);

            Quas = Owner.GetAbilityById(AbilityId.invoker_quas);
            Wex = Owner.GetAbilityById(AbilityId.invoker_wex);
            Exort = Owner.GetAbilityById(AbilityId.invoker_exort);

            SunStrike = new AbilityInfo(Exort, Exort, Exort, Owner.GetAbilityById(AbilityId.invoker_sun_strike));
            ColdSnap = new AbilityInfo(Quas, Quas, Quas, Owner.GetAbilityById(AbilityId.invoker_cold_snap));
            Alacrity = new AbilityInfo(Wex, Wex, Exort, Owner.GetAbilityById(AbilityId.invoker_alacrity));
            Meteor = new AbilityInfo(Exort, Exort, Wex, Owner.GetAbilityById(AbilityId.invoker_chaos_meteor));
            Blast = new AbilityInfo(Quas, Exort, Wex, Owner.GetAbilityById(AbilityId.invoker_deafening_blast));
            ForgeSpirit = new AbilityInfo(Exort, Exort, Quas, Owner.GetAbilityById(AbilityId.invoker_forge_spirit));
            GhostWalk = new AbilityInfo(Quas, Quas, Wex, Owner.GetAbilityById(AbilityId.invoker_ghost_walk));
            IceWall = new AbilityInfo(Quas, Quas, Exort, Owner.GetAbilityById(AbilityId.invoker_ice_wall));
            Tornado = new AbilityInfo(Wex, Wex, Quas, Owner.GetAbilityById(AbilityId.invoker_tornado));
            Emp = new AbilityInfo(Wex, Wex, Wex, Owner.GetAbilityById(AbilityId.invoker_emp));

            AbilityInfos = new List<AbilityInfo>
            {
                SunStrike,
                ColdSnap,
                Alacrity,
                Meteor,
                Blast,
                ForgeSpirit,
                GhostWalk,
                IceWall,
                Tornado,
                Emp
            };

            Empty1 = Owner.GetAbilityById(AbilityId.invoker_empty1);
            Empty2 = Owner.GetAbilityById(AbilityId.invoker_empty2);
            InvokeAbility = Owner.GetAbilityById(AbilityId.invoker_invoke);
        }

        public Ability InvokeAbility { get; set; }

        public Ability Empty2 { get; set; }

        public Ability Empty1 { get; set; }

        public AbilityInfo Emp { get; set; }

        public AbilityInfo IceWall { get; set; }

        public AbilityInfo Tornado { get; set; }

        public AbilityInfo GhostWalk { get; set; }

        public AbilityInfo ForgeSpirit { get; set; }

        public AbilityInfo Blast { get; set; }

        public AbilityInfo Meteor { get; set; }

        public AbilityInfo Alacrity { get; set; }

        public AbilityInfo ColdSnap { get; set; }

        public AbilityInfo SunStrike { get; set; }

        public Ability Exort { get; set; }

        public Ability Wex { get; set; }

        public Ability Quas { get; set; }

        [ItemBinding]
        public item_cyclone Eul { get; set; }
        [ItemBinding]
        public item_shivas_guard Shiva { get; set; }
        [ItemBinding]
        public item_sheepstick Hex { get; set; }
        [ItemBinding]
        public item_blink Blink { get; set; }

        public List<AbilityInfo> AbilityInfos;

        protected override void OnActivate()
        {
            Config = new Config(this);
            Log.Debug("new config");
            Config.ComboKey.Item.ValueChanged += HotkeyChanged;
            Log.Debug("event to config");
            OrbwalkerManager.Value.Activate();
            Log.Debug("activate OrbwalkerManager");
            TargetManager.Value.Activate();
            Log.Debug("activate TargetManager");
            _mode.UpdateConfig(Config);
            Log.Debug("load config");
            OrbwalkerManager.Value.RegisterMode(_mode);
            Log.Debug("RegisterMode");
            _mode.Load();
            Log.Debug($"_mode loaded. Key for combo -> {_mode.Key}");
            InventoryManager.Value.Attach(this);
            Log.Debug("InventoryManager Attach");
            SelectedCombo = 0;
            InventoryManager.Value.CollectionChanged += ValueOnCollectionChanged;
            //if (InventoryManager.Value.Inventory.Items.Any(x => x.Id == AbilityId.item_cyclone))
            if (Eul!=null)
            {
                _eulCombo1 = new Combo(this, new[]
                {
                    new AbilityInfo(Eul.Ability), SunStrike, Meteor, Blast, ColdSnap, Alacrity, ForgeSpirit
                });
                _eulCombo2 = new Combo(this, new[]
                {
                    new AbilityInfo(Eul.Ability), Meteor, Blast, ColdSnap, Alacrity, ForgeSpirit
                });
                _eulCombo3 = new Combo(this, new[]
                {
                    new AbilityInfo(Eul.Ability), SunStrike, IceWall, ColdSnap, Alacrity, ForgeSpirit
                });
                Config.ComboPanel.Combos.Add(_eulCombo1);
                Config.ComboPanel.Combos.Add(_eulCombo2);
                Config.ComboPanel.Combos.Add(_eulCombo3);
            }
        }

        private Combo _eulCombo1;
        private Combo _eulCombo2;
        private Combo _eulCombo3;
        private void ValueOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (InventoryItem argsNewItem in args.NewItems)
                    {
                        if (argsNewItem.Id == AbilityId.item_cyclone)
                        {
                            _eulCombo1 = new Combo(this, new[]
                            {
                                new AbilityInfo(Eul.Ability), SunStrike, Meteor, Blast, ColdSnap, Alacrity, ForgeSpirit
                            });
                            _eulCombo2 = new Combo(this, new[]
                            {
                                new AbilityInfo(Eul.Ability), Meteor, Blast, ColdSnap, Alacrity, ForgeSpirit
                            });
                            _eulCombo3 = new Combo(this, new[]
                            {
                                new AbilityInfo(Eul.Ability), SunStrike, IceWall, ColdSnap, Alacrity, ForgeSpirit
                            });
                            Config.ComboPanel.Combos.Add(_eulCombo1);
                            Config.ComboPanel.Combos.Add(_eulCombo2);
                            Config.ComboPanel.Combos.Add(_eulCombo3);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (InventoryItem argsNewItem in args.OldItems)
                    {
                        if (argsNewItem.Id == AbilityId.item_cyclone)
                        {
                            Config.ComboPanel.Combos.Remove(_eulCombo1.Dispose());
                            Config.ComboPanel.Combos.Remove(_eulCombo2.Dispose());
                            Config.ComboPanel.Combos.Remove(_eulCombo3.Dispose());
                        }
                    }
                    break;
            }
        }

        protected override void OnDeactivate()
        {
            OrbwalkerManager.Value.UnregisterMode(_mode);
            Log.Debug("OrbwalkerManager UnregisterMode");
            OrbwalkerManager.Value.Deactivate();
            Log.Debug("OrbwalkerManager deactivated");
            TargetManager.Value.Deactivate();
            Log.Debug("TargetManager deactivated");
            Config?.Dispose();
            Log.Debug("Config deactivated");
            _mode.Unload();
            Log.Debug("_mode unloaded");
            InventoryManager.Value.Detach(this);
            Log.Debug("InventoryManager Detach");
            InventoryManager.Value.CollectionChanged -= ValueOnCollectionChanged;
        }

        private void HotkeyChanged(object sender, OnValueChangeEventArgs e)
        {
            var keyCode = e.GetNewValue<KeyBind>().Key;
            if (keyCode == e.GetOldValue<KeyBind>().Key)
            {
                return;
            }
            var key = KeyInterop.KeyFromVirtualKey((int)keyCode);
            _mode.Key = key;
            Log.Debug("new hotkey: " + key);
        }

        public async Task<bool> Invoke(AbilityInfo info)
        {
            if (!InvokeAbility.CanBeCasted())
            {
                Log.Error($"can't invoke (cd) {(int)InvokeAbility.Cooldown+1}");
                return false;
            }
            var sphereDelay = 50;
            info.One.UseAbility();
            await Await.Delay(sphereDelay);
            info.Two.UseAbility();
            await Await.Delay(sphereDelay);
            info.Three.UseAbility();
            await Await.Delay(sphereDelay);
            InvokeAbility.UseAbility();
            Log.Error($"invoke: [{info.Ability.Name}]");
            await Await.Delay((int) Game.Ping+75);
            return true;
        }
    }
}