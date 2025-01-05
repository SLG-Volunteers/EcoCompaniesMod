﻿using System;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;


namespace Eco.Mods.Companies
{
    using Server;
    using Core.Plugins.Interfaces;
    using Core.Utils;
    using Core.Systems;
    using Core.Serialization;
    using Core.Plugins;
    using Core.Controller;

    using Shared.Localization;
    using Shared.Utils;
    using Shared.Serialization;
    using Shared.Networking;
    using Shared.IoC;
    using Shared.Time;

    using Gameplay.Players;
    using Gameplay.Systems;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Civics.GameValues;
    using Gameplay.Civics.Laws;
    using Gameplay.Aliases;
    using Gameplay.Items;
    using Gameplay.Items.InventoryRelated;
    using Gameplay.GameActions;
    using Gameplay.Utils;
    using Gameplay.Economy;
    using Gameplay.Economy.Transfer;
    using Gameplay.Settlements.Civics;
    using Gameplay.Property;

    using Simulation.Time;

    [Localized]
    public class CompaniesConfig
    {
        [LocDescription("If enabled, employees may not have homestead deeds and holdings, and the company gets a HQ homestead deed that grows based on employee count."), Category("Property")]
        public bool PropertyLimitsEnabled { get; set; } = true;

        [LocDescription("If enabled, the legal person of a company can't receive reputation (this does not include the 'ReputationAverages')."), Category("Reputation")]
        public bool DenyLegalPersonReputationEnabled { get; set; } = false;

        [LocDescription("If enabled, the company members can't receive reputation."), Category("Reputation")]
        public bool DenyCompanyMembersExternalReputationEnabled { get; set; } = false;

        [LocDescription("If enabled, the company members can't give reputation to each other nor the legal person (also counts for invited members)."), Category("Reputation")]
        public bool DenyCompanyMembersReputationEnabled { get; set; } = false;

        [LocDescription("If enabled, the average repuation from all employees will be given to the legal person (in addition to their own reputation if they have any)."), Category("Reputation")]
        public bool ReputationAveragesEnabled { get; set; } = false;

        [LocDescription("If enabled, the average repuation from all employees will be filtered by known bonussources (currently only SpeaksWellOfOthersBonus)."), Category("Reputation")]
        public bool ReputationAveragesBonusEnabled { get; set; } = true;

        [LocDescription("If enabled, the company vehicles will be adopted to the legal person on placement (need PropertyLimitesEnabled to be also enabled)."), Category("Property")]
        public bool VehicleTransfersEnabled { get; set; } = false;

        [LocDescription("If enabled, the company name instead of the legal persons name will be used for naming (shorter)"), Category("Property")]
        public bool VehicleTransfersUseCompanyNameEnabled { get; set; } = true;
    }

    [Serialized]
    public class CompaniesData : Singleton<CompaniesData>, IStorage
    {
        public IPersistent StorageHandle { get; set; }

        [Serialized] public Registrar<Company> Companies = new();

        public readonly PeriodicUpdateConfig UpdateTimer = new(true);

        public void InitializeRegistrars()
        {
            this.Companies.PreInit(Localizer.DoStr("Companies"), true, CompaniesPlugin.Obj, Localizer.DoStr("Companies"));
        }

        public void Initialize()
        {
            
        }
    }

    [Eco]
    internal class CompanyLawManager : ILawManager, IController, IHasClientControlledContainers
    {
        private readonly LawManager internalLawManager;

        public CompanyLawManager(LawManager internalLawManager)
        {
            this.internalLawManager = internalLawManager;
        }

        public PostResult Perform(GameAction action, AccountChangeSet acc)
        {
            var result = internalLawManager.Perform(action, acc);
            switch (action)
            {
                case StartHomestead startHomesteadAction:
                    CompanyManager.Obj.InterceptStartHomesteadGameAction(startHomesteadAction, ref result);
                    break;
                case PlaceOrPickUpObject placeOrPickupObjectAction:
                    CompanyManager.Obj.InterceptPlaceOrPickupObjectGameAction(placeOrPickupObjectAction, ref result);
                    break;
                case ReputationTransfer reputationTransferAction: // intercepts new reputation actions
                    CompanyManager.Obj.InterceptReputationTransfer(reputationTransferAction, ref result);
                    break;
                case TradeAction tradeAction:
                    CompanyManager.Obj.InterceptTradeAction(tradeAction, ref result);
                    break;
            }
            return result;
        }

        #region IController
        public ref int ControllerID => ref internalLawManager.ControllerID;
        #endregion
    }

    [Localized, LocDisplayName(nameof(CompaniesPlugin)), Priority(PriorityAttribute.High)]
    public class CompaniesPlugin : Singleton<CompaniesPlugin>, IModKitPlugin, IConfigurablePlugin, IInitializablePlugin, ISaveablePlugin, IContainsRegistrars
    {
        private bool ignoreBankAccountPermissionsChanged = false;
        public const int TaskDelay = 250;
        public const int TaskDelayLong = 1000;
        public const double DailyPlayTime = (TimeUtil.SecondsPerMinute * 5);

        public IPluginConfig PluginConfig => config;

        private PluginConfig<CompaniesConfig> config;
        public CompaniesConfig Config => config.Config;

        private static readonly Dictionary<Type, GameValueType> gameValueTypeCache = new();

        public readonly CompanyManager CompanyManager;

        [NotNull] private readonly CompaniesData data;

        public CompaniesPlugin()
        {
            config = new PluginConfig<CompaniesConfig>("Companies");
            data = StorageManager.LoadOrCreate<CompaniesData>("Companies");
            CompanyManager = new CompanyManager();
        }

        public object GetEditObject() => Config;
        public ThreadSafeAction<object, string> ParamChanged { get; set; } = new ThreadSafeAction<object, string>();

        public void OnEditObjectChanged(object o, string param)
        {
            this.SaveConfig();
        }

        public void Initialize(TimedTask timer)
        {
            data.Initialize();
            Singleton<PluginManager>.Obj.InitComplete += OnPostInitialize;

            InstallLawManagerHack();
            InstallGameValueHack();

            BankAccount.CurrencyHoldingsChangedEvent.Add(OnCurrencyHoldingsChanged);
            BankAccount.PermissionsChangedEvent.Add(OnBankAccountPermissionsChanged);
            GameData.Obj.VoidStorageManager.VoidStorages.Callbacks.OnAdd.Add(OnVoidStorageAdded);
            PropertyManager.DeedDestroyedEvent.Add(OnDeedDestroyed);
            PropertyManager.DeedOwnerChangedEvent.Add(OnDeedOwnerChanged);
            UserManager.OnUserLoggedOut.Add(OnUserLoggedOut);
        }

        internal static void OnPostInitialize()
        {
            Registrars.Get<Company>().ForEach(company => company.OnPostInitialized());

        }

        private static void OnUserLoggedOut(User user)
        {
            var userEmployer = Company.GetEmployer(user);
            userEmployer?.UpdateOnlineState();

        }

        private void OnCurrencyHoldingsChanged(BankAccount bankAccount)
        {
            Company.GetEmployer(bankAccount.AccountOwner)?.OnEmployeeWealthChange(bankAccount);
        }

        private void OnBankAccountPermissionsChanged(BankAccount bankAccount)
        {
            if (ignoreBankAccountPermissionsChanged) { return; }
            if (bankAccount == null || bankAccount.DualPermissions == null) { return; }
            var company = Company.GetFromBankAccount(bankAccount);
            if (company == null) { return; }
            try
            {
                ignoreBankAccountPermissionsChanged = true;
                // Logger.Debug($"Got OnBankAccountPermissionsChanged for {bankAccount.Name}, setting ownership to {company.Name}");
                company.UpdateBankAccountAuthList(bankAccount);
            }
            finally
            {
                ignoreBankAccountPermissionsChanged = false;
            }
        }

        private void OnVoidStorageAdded(INetObject netObj, object obj)
        {
            if (obj is not VoidStorageWrapper voidStorage) { return; }
            foreach (var alias in voidStorage.CanAccess)
            {
                var company = Company.GetFromLegalPerson(alias);
                if (company == null) { continue; }
                company.OnLegalPersonGainedVoidStorage(voidStorage);
            }
        }

        private void OnDeedDestroyed(Deed deed, User performer)
        {
            CompanyManager.HandleDeedDestroyed(deed);
        }

        private void OnDeedOwnerChanged(Deed deed)
        {
            if (deed.Destroying || deed.IsDestroyed) { return; }
            CompanyManager.HandleDeedOwnerChanged(deed);
        }

        private void InstallLawManagerHack()
        {
            var oldLawManager = ServiceHolder<ILawManager>.Obj;
            if (oldLawManager is LawManager oldLawManagerConcrete)
            {
                ServiceHolder<ILawManager>.Obj = new CompanyLawManager(oldLawManagerConcrete);
            }
            else
            {
                Logger.Error($"Failed to install law manager hack: ServiceHolder<ILawManager>.Obj was not of expected type");
            }
        }

        private void InstallGameValueHack()
        {
            var attr = typeof(GameValue).GetCustomAttribute<CustomRPCSetterAttribute>();
            attr.ContainerType = GetType();
            attr.MethodName = nameof(DynamicSetGameValue);
            var idToMethod = typeof(RPCManager).GetField("IdToMethod", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as Dictionary<int, RPCMethod>;
            if (idToMethod == null)
            {
                Logger.Error($"Failed to install game value hack: Couldn't retrieve RPCManager.IdToMethod");
            }
            var rpcMethodFuncProperty = typeof(RPCMethod).GetProperty(nameof(RPCMethod.Func), BindingFlags.Public | BindingFlags.Instance);
            if (rpcMethodFuncProperty == null)
            {
                Logger.Error($"Failed to install game value hack: Couldn't find RPCMethod.Func property");
                return;
            }
            var backingField = GetBackingField(rpcMethodFuncProperty);
            if (backingField == null)
            {
                Logger.Error($"Failed to install game value hack: Couldn't find RPCMethod.Func backing field");
                return;
            }
            var relevantRpcMethods = idToMethod.Values
                .Where(x => x.IsCustomSetter && x.PropertyInfo != null)
                .Where(x => x.PropertyInfo.PropertyType.IsAssignableTo(typeof(GameValue<IAlias>)) || x.PropertyInfo.PropertyType.IsAssignableTo(typeof(GameValue<User>)));
            foreach (var rpcMethod in relevantRpcMethods)
            {
                Func<object, object[], object> overrideFunc = (target, args) => { DynamicSetGameValue(target, rpcMethod.PropertyInfo, args[0]); return null; };
                backingField.SetValue(rpcMethod, overrideFunc);
            }
        }

        private static FieldInfo GetBackingField(PropertyInfo pi)
        {
            if (!pi.CanRead || !pi.GetGetMethod(nonPublic: true).IsDefined(typeof(CompilerGeneratedAttribute), inherit: true))
                return null;
            var backingField = pi.DeclaringType.GetField($"<{pi.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (backingField == null)
                return null;
            if (!backingField.IsDefined(typeof(CompilerGeneratedAttribute), inherit: true))
                return null;
            return backingField;
        }

        public static void DynamicSetGameValue(object parent, PropertyInfo prop, object newValue)
        {
            if (newValue is BSONObject obj) { newValue = BsonManipulator.FromBson(obj, typeof(IController)); }
            if (newValue is GameValueType gvt)
            {
                if (gvt.Type == typeof(AccountLegalPerson) && prop.PropertyType == typeof(GameValue<IAlias>))
                {
                    // The property wants an IAlias and the client sent an AccountLegalPerson, so remap it to AccountLegalPersonAlias
                    newValue = GetGameValueType<AccountLegalPersonAlias>();
                }
                else if (gvt.Type == typeof(AccountLegalPersonAlias) && prop.PropertyType == typeof(GameValue<User>))
                {
                    // The property wants a User and the client sent an AccountLegalPersonAlias, so remap it to AccountLegalPerson
                    // This shouldn't really be possible as AccountLegalPersonAlias is marked as NonSelectable but do it anyway to be safe
                    newValue = GetGameValueType<AccountLegalPerson>();
                }
                else if (gvt.Type == typeof(EmployerLegalPerson) && prop.PropertyType == typeof(GameValue<IAlias>))
                {
                    // The property wants an IAlias and the client sent an EmployerLegalPerson, so remap it to EmployerLegalPersonAlias
                    newValue = GetGameValueType<EmployerLegalPersonAlias>();
                }
                else if (gvt.Type == typeof(EmployerLegalPersonAlias) && prop.PropertyType == typeof(GameValue<User>))
                {
                    // The property wants a User and the client sent an EmployerLegalPersonAlias, so remap it to EmployerLegalPerson
                    // This shouldn't really be possible as EmployerLegalPersonAlias is marked as NonSelectable but do it anyway to be safe
                    newValue = GetGameValueType<EmployerLegalPerson>();
                }
                else if (gvt.Type == typeof(CompanyCeo) && prop.PropertyType == typeof(GameValue<IAlias>))
                {
                    // The property wants an IAlias and the client sent an CompanyCeo, so remap it to CompanyCeoAlias
                    newValue = GetGameValueType<CompanyCeoAlias>();
                }
                else if (gvt.Type == typeof(CompanyCeoAlias) && prop.PropertyType == typeof(GameValue<User>))
                {
                    // The property wants a User and the client sent an CompanyCeoAlias, so remap it to CompanyCeo
                    // This shouldn't really be possible as CompanyCeoAlias is marked as NonSelectable but do it anyway to be safe
                    newValue = GetGameValueType<CompanyCeo>();
                }
            }
            GameValueManager.DynamicSetGameValue(parent, prop, newValue);
        }

        private static GameValueType GetGameValueType<T>() where T : GameValue
            => gameValueTypeCache.GetOrAdd(typeof(T), () => new GameValueType()
                {
                    Type = typeof(T),
                    ChoosesType = typeof(T).GetStaticPropertyValue<Type>("ChoosesType"), // note: ignores Derived attribute
                    ContextRequirements = typeof(T).Attribute<RequiredContextAttribute>()?.RequiredTypes,
                    Name = typeof(T).Name,
                    Description = typeof(T).GetLocDescription(),
                    Category = typeof(T).Attribute<LocCategoryAttribute>()?.Category,
                    MarkedUpName = typeof(T).UILink(),
                });

        public void InitializeRegistrars(TimedTask timer) => data.InitializeRegistrars();
        public string GetDisplayText() => string.Empty;
        public string GetCategory() => Localizer.DoStr("Civics");
        public string GetStatus() => string.Empty;
        public override string ToString() => Localizer.DoStr("Companies");
        public void SaveAll() => StorageManager.Obj.MarkDirty(data);
    }
}