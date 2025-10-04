using System.Collections.Generic;
using System.Linq;

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

    [Eco, LocCategory("Companies"), LocDescription("Triggered when money is transfered from an employee to the company (Private Property Ban)."), HasIcon]
    public class PrivatePropertyBan : GameAction
    {
        [Eco, LocDescription("The affected citizen (sender)."), CanAutoAssign] public User Citizen { get; set; }
        [Eco, LocDescription("The legal person of the company (receiver)."), CanAutoAssign] public User CompanyLegalPerson { get; set; }
        [Eco, LocDescription("The amount of money that was seized."), CanAutoAssign] public float CurrencyAmount { get; set; } 
        [Eco, LocDescription("The affected currency."), CanAutoAssign] public Currency Currency { get; set; }
        [Eco, LocDescription("The TaxCode"), CanAutoAssign] public string TaxCode { get; set; }

        public override IEnumerable<Settlement> SettlementScopes => CompanyLegalPerson?.AllCitizenships ?? Enumerable.Empty<Settlement>(); // Scope based on company citizenship
    }
}
