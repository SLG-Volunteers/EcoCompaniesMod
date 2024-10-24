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

    [Eco, LocCategory("Companies"), LocDescription("Triggered when an employee receives currency to the bank account."), CannotBePrevented]
    public class CompanyEmployeeIncome : MoneyGameAction
    {
        [Eco, LocDescription("The bank account the money came from."), CanAutoAssign] public override BankAccount SourceBankAccount { get; set; }
        [Eco, LocDescription("The bank account the money went to.")] public override BankAccount TargetBankAccount { get; set; }
        [Eco, LocDescription("The currency of the transfer."), CanAutoAssign] public override Currency Currency { get; set; }
        [Eco, LocDescription("The amount of money transfered.")] public override float CurrencyAmount { get; set; }
        [Eco, LocDescription("The citizen of the company who received the money."), CanAutoAssign] public User ReceiverCitizen { get; set; }

        public override IEnumerable<Settlement> SettlementScopes => ReceiverCitizen?.AllCitizenships ?? Enumerable.Empty<Settlement>(); //Scope based on company citizenship
    }
}
