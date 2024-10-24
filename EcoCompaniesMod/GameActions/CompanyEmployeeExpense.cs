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

    [Eco, LocCategory("Companies"), LocDescription("Triggered when an employee spents currency to the bank account."), CannotBePrevented]
    public class CompanyEmployeeExpense : MoneyGameAction
    {
        [Eco, LocDescription("The bank account the money came from."), CanAutoAssign] public override BankAccount SourceBankAccount { get; set; }
        [Eco, LocDescription("The bank account the money went to.")] public override BankAccount TargetBankAccount { get; set; }
        [Eco, LocDescription("The currency of the transfer."), CanAutoAssign] public override Currency Currency { get; set; }
        [Eco, LocDescription("The amount of money transfered.")] public override float CurrencyAmount { get; set; }
        [Eco, LocDescription("The citizen of the company who spent the money."), CanAutoAssign] public User SendingCitizen { get; set; }

        public override IEnumerable<Settlement> SettlementScopes => SendingCitizen?.AllCitizenships ?? Enumerable.Empty<Settlement>(); //Scope based on company citizenship
    }
}
