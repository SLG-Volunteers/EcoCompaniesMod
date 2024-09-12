namespace Eco.Mods.Companies
{
    using Core.Plugins.Interfaces;

    public class CompaniesMod : IModInit
    {
        public static ModRegistration Register() => new()
        {
            ModName = "Companies",
            ModDescription = "Extends the law and economy system with player controllable companies.",
            ModDisplayName = "Companies",
        };
    }
}
