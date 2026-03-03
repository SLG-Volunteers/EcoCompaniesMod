namespace Eco.Mods.Companies
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    using Gameplay.Civics.GameValues;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;
    using System;
    using System.Linq;

    [Eco, LocCategory("Companies"), LocDescription("The number of employees of a company, including the CEO.")]
    public class EmployeeCount : GameValue<float>
    {
        [Eco, Advanced, LocDescription("The legal person whose company's employee count is being evaluated.")] public GameValue<User> LegalPerson { get; set; }
        [Eco, Advanced, LocDescription("Whether to count only active employees.")] public GameValue<bool> OnlyActive { get; set; } = new No();

        private Eval<float> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", float.MinValue)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", float.MinValue);

        public override Eval<float> Value(IContextObject action)
        {
            var legalPerson = this.LegalPerson?.Value(action); if (legalPerson?.Val == null) return this.FailNullSafeFloat(legalPerson, nameof(this.LegalPerson));
            var onlyActive = this.OnlyActive?.Value(action).Val ?? false;

            var company = Company.GetFromLegalPerson(legalPerson.Val);
            if (company == null) return this.FailNullSafeFloat(legalPerson, nameof(this.LegalPerson));
            float employeeCount = onlyActive == true ? company.AllEmployees.Count(e => !e.IsAbandoned) : company.AllEmployees.Count();

            return Eval.Make(new LocString($"{Text.StyledNum(employeeCount)} (" + (onlyActive ? $"active " : $"") + $"employee count of {company.UILink()})"), employeeCount);
        }

        public override LocString Description() => Localizer.Do($"employee count of company of {LegalPerson.DescribeNullSafe()}");
    }
}
