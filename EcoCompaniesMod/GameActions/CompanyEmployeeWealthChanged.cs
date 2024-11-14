using System.Collections.Generic;

namespace Eco.Mods.Companies.GameActions
{
    using Gameplay.Players;
    using Gameplay.Economy;
    using Gameplay.GameActions;
    using Gameplay.Civics;
    using Gameplay.Settlements;

    using Shared.Localization;
    using Shared.Networking;
    using System.Linq;
    using Eco.Core.Controller;

    [Eco, LocCategory("Companies"), LocDescription("Triggered when an employees' holdings change."), HasIcon("Tax"), CannotBePrevented]
    public class CompanyEmployeeWealthChanged : GameAction
    {
        [Eco, LocDescription("The affected bank account.")] public BankAccount TargetBankAccount { get; set; }
        [Eco, LocDescription("The affected employee."), CanAutoAssign] public User AffectedCitizen { get; set; }

        public override IEnumerable<Settlement> SettlementScopes => AffectedCitizen?.AllCitizenships ?? Enumerable.Empty<Settlement>(); //Scope based on company citizenship
    }
}
