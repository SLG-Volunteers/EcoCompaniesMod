﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eco.Mods.Companies
{
    using Core.Systems;
    using Core.Utils;

    using Gameplay.Players;
    using Gameplay.GameActions;
    using Gameplay.Property;
    using Gameplay.Systems.NewTooltip;
    using Gameplay.Systems.Messaging.Notifications;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Settlements.ClaimStakes;
    using Gameplay.Civics.GameValues;
    using Gameplay.Auth;
    using Gameplay.Aliases;
    using Gameplay.Settlements;
    using Gameplay.Settlements.Civics;
    using Gameplay.Items;

    using Shared.Utils;
    using Shared.Localization;
    using Shared.Items;
    using Shared.Services;

    public partial class CompanyManager : Singleton<CompanyManager>, IGameActionAware
    {
        [GeneratedRegex("^[\\w][\\w_'. ]+$")]
        private static partial Regex ValidCompanyNameRegex();

        public CompanyManager()
        {
            ActionUtil.AddListener(this);
        }

        public bool ValidateName(string name, out string errorMessage)
        {
            if (name.Length < 3)
            {
                errorMessage = Localizer.DoStr("Company name is too short, must be at least 3 characters long");
                return false;
            }
            if (name.Length > 50)
            {
                errorMessage = Localizer.DoStr("Company name is too long, must be at most 50 characters long");
                return false;
            }
            if (!ValidCompanyNameRegex().IsMatch(name))
            {
                errorMessage = Localizer.DoStr("Company name contains invalid characters, must only contain letters, digits, underscores, apostrophies or full stops, and must start with a character.");
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }

        public readonly struct CreateAttempt : IEquatable<CreateAttempt>
        {
            public static readonly CreateAttempt Invalid = new();

            public readonly User CEO;
            public readonly string CompanyName;
            public readonly IEnumerable<Deed> TransferDeeds;
            public readonly Settlement JoinSettlement;

            public bool IsValid => CEO != null && !string.IsNullOrEmpty(CompanyName);

            public CreateAttempt(User ceo, string companyName, IEnumerable<Deed> transferDeeds, Settlement joinSettlement)
            {
                CEO = ceo;
                CompanyName = companyName;
                TransferDeeds = transferDeeds;
                JoinSettlement = joinSettlement;
            }

            public override bool Equals(object obj) => obj is CreateAttempt attempt && Equals(attempt);

            public bool Equals(CreateAttempt other)
                => CEO == other.CEO
                && CompanyName == other.CompanyName
                && TransferDeeds.SetEquals(other.TransferDeeds)
                && JoinSettlement == other.JoinSettlement;

            public override int GetHashCode() => HashCode.Combine(CEO, CompanyName, TransferDeeds);

            public static bool operator ==(CreateAttempt left, CreateAttempt right) => left.Equals(right);

            public static bool operator !=(CreateAttempt left, CreateAttempt right) => !(left == right);

            public LocString ToLocString()
                => Localizer.Do($"This will found a company named '{CompanyName}' with {CEO.UILinkNullSafe()} as the CEO.\n{DescribeTransfers()}\n{DescribeJoinSettlement()}");

            private LocString DescribeTransfers()
            {
                if (TransferDeeds?.Any() ?? false)
                {
                    return Localizer.Do($"The following deeds will be transferred to the company upon founding: {TransferDeeds.Select(x => x.UILinkNullSafe()).InlineFoldoutListLoc("deed", TooltipOrigin.None, 5)}");
                }
                return Localizer.DoStr("No deeds will be transferred to the company upon founding.");
            }

            private LocString DescribeJoinSettlement()
            {
                if (JoinSettlement != null)
                {
                    if (JoinSettlement.ImmigrationPolicy?.Approver == null)
                    {
                        return Localizer.Do($"The company will join {JoinSettlement.UILink()} upon founding.");
                    }
                    else
                    {
                        return Localizer.Do($"The company will apply to join {JoinSettlement.UILink()} upon founding.");
                    }
                }
                return Localizer.Do($"The company will not be considered a citizen of any settlement upon founding.");
            }
        }

        public CreateAttempt CreateNewDryRun(User ceo, string name, out string errorMessage)
        {
            var existingEmployer = Company.GetEmployer(ceo);
            if (existingEmployer != null)
            {
                errorMessage = $"Couldn't found a company as you're already a member of {existingEmployer}";
                return CreateAttempt.Invalid;
            }

            name = name.Trim().ProfanityFiltered();
            if (!ValidateName(name, out errorMessage)) { return CreateAttempt.Invalid; }
            name = Registrars.Get<Company>().GetUniqueName(name);

            if (Registrars.Get<Company>().GetByName(name) != null)
            {
                errorMessage = $"A company with the name '{name}' already exists";
                return CreateAttempt.Invalid;
            }

            if (Registrars.Get<User>().GetByName(GetLegalPersonName(name)) != null)
            {
                errorMessage = $"A company with the name '{name}' already exists";
                return CreateAttempt.Invalid;
            }

            errorMessage = string.Empty;
            return new CreateAttempt(
                ceo, name,
                CompaniesPlugin.Obj.Config.PropertyLimitsEnabled && ceo.HomesteadDeed != null ? Enumerable.Repeat(ceo.HomesteadDeed, 1) : Enumerable.Empty<Deed>(),
                CompaniesPlugin.Obj.Config.PropertyLimitsEnabled ? ceo.DirectCitizenship : null
            );
        }

        public Company CreateNew(User ceo, string name, CreateAttempt createAttempt, out string errorMessage)
        {
            var latestCreateAttempt = CreateNewDryRun(ceo, name, out errorMessage);
            if (!latestCreateAttempt.IsValid) { return null; }
            if (latestCreateAttempt != createAttempt)
            {
                errorMessage = $"Something changed since you tried to create the company. Please try again.";
                return null;
            }
            var company = Registrars.Add<Company>(null, latestCreateAttempt.CompanyName);
            company.Creator = latestCreateAttempt.CEO;
            company.ChangeCeo(latestCreateAttempt.CEO);
            // TODO: Assign company citienzehip to CEO's federation
            company.SaveInRegistrar();
            if (latestCreateAttempt.TransferDeeds != null)
            {
                foreach (var deed in latestCreateAttempt.TransferDeeds)
                {
                    ClaimHomesteadAsHQ(ceo, deed, company);
                }
            }
            company.UpdateAllAuthLists();
            NotificationManager.ServerMessageToAll(
                Localizer.Do($"{ceo.UILink()} has founded the company {company.UILink()}!"),
                NotificationCategory.Government,
                NotificationStyle.Chat
            );
            if (company.DirectCitizenship == null && createAttempt.JoinSettlement != null)
            {
                if (!company.TryApplyToSettlement(ceo, createAttempt.JoinSettlement, out var joinErr))
                {
                    Logger.Debug($"Company {company.Name} tried to apply to {createAttempt.JoinSettlement.Name} during founding process but failed: '{joinErr}'");
                }
            }
            return company;
        }

        public void ActionPerformed(GameAction action)
        {
            try
            {
                switch (action)
                {
                    case GameActions.CompanyExpense:
                    case GameActions.CompanyIncome:
                        // Catch these specifically and noop, to avoid them going into the MoneyGameAction case
                        break;
                    case MoneyGameAction moneyGameAction:
                        var sourceCompany = Company.GetFromBankAccount(moneyGameAction.SourceBankAccount);
                        sourceCompany?.OnGiveMoney(moneyGameAction);
                        var destCompany = Company.GetFromBankAccount(moneyGameAction.TargetBankAccount);
                        destCompany?.OnReceiveMoney(moneyGameAction);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CompanyManager.ActionPerformed had an exception while handling a {action?.GetType()}: {ex}");
                Logger.Error(ex.StackTrace);
            }
        }

        public LazyResult ShouldOverrideAuth(IAlias alias, IOwned property, GameAction action)
        {
            switch (action)
            {
                case PropertyTransfer propertyTransferAction:
                    {
                        // If the deed is company property, allow an employee to transfer ownership, UNLESS it's their HQ
                        Company deedOwnerCompany = null;
                        foreach (var deed in propertyTransferAction.RelatedDeeds)
                        {
                            var ownerCompany = Company.GetFromLegalPerson(deed.Owners);
                            if (ownerCompany == null || deedOwnerCompany != null && ownerCompany != deedOwnerCompany)
                            {
                                deedOwnerCompany = null;
                                break;
                            }
                            deedOwnerCompany = ownerCompany;
                        }
                        if (deedOwnerCompany == null) { return LazyResult.FailedNoMessage; }
                        if (!deedOwnerCompany.IsEmployee(propertyTransferAction.Citizen)) { return LazyResult.FailedNoMessage; }
                        if (CompaniesPlugin.Obj.Config.PropertyLimitsEnabled && deedOwnerCompany.HQDeed != null && propertyTransferAction.RelatedDeeds.Contains(deedOwnerCompany.HQDeed)) { return LazyResult.FailedNoMessage; }
                        return AuthManagerExtensions.SpecialAccessResult(deedOwnerCompany);
                    }

                case ClaimOrUnclaimProperty claimOrUnclaimPropertyAction:
                    {
                        // If the deed is company property, allow an employee to claim or unclaim it
                        var deedOwnerCompany = Company.GetFromLegalPerson(claimOrUnclaimPropertyAction.PreviousDeedOwner);
                        if (deedOwnerCompany == null) { return LazyResult.FailedNoMessage; }
                        if (!deedOwnerCompany.IsEmployee(claimOrUnclaimPropertyAction.Citizen)) { return LazyResult.FailedNoMessage; }
                        return AuthManagerExtensions.SpecialAccessResult(deedOwnerCompany);
                    }
                default:
                    return LazyResult.FailedNoMessage;
            }
        }
        public void InterceptTradeAction(TradeAction tradeActionData, ref PostResult lawPostResult)
        {
            var targetCompany = Company.GetFromLegalPerson(tradeActionData.ShopOwner);
            if(targetCompany != null)
            {
                var boughtOrSold = tradeActionData.BoughtOrSold == BoughtOrSold.Selling ? "sold" : "bought";
                targetCompany.SendCompanyMessage(Localizer.Do($"{targetCompany.UILinkNullSafe()}: {tradeActionData.Citizen.UILinkNullSafe()} {boughtOrSold} {tradeActionData.NumberOfItems} {tradeActionData.ItemUsed.UILinkNullSafe()} at {tradeActionData.WorldObject.UILinkNullSafe()}"), NotificationCategory.YourTrades);
            }
        }

        public void InterceptReputationTransfer(ReputationTransfer reputationTransferData, ref PostResult lawPostResult)
        {
            if (reputationTransferData.TargetType == ReputationTargetType.ReputationGivenToPicture) { return; }

            var senderCompany = Company.GetEmployer(reputationTransferData.ReputationSender);
            var receiverEmployeer = Company.GetEmployer(reputationTransferData.ReputationReceiver);

            if (CompaniesPlugin.Obj.Config.DenyLegalPersonReputationEnabled) // we do not allow anybody to honor the company legal person if settings are matching
            {
                var receiverIsLegalPerson = Company.GetFromLegalPerson(reputationTransferData.ReputationReceiver);
                if (receiverIsLegalPerson != null)
                {
                    lawPostResult.Success = false;
                    NotificationManager.ServerMessageToPlayer(
                        Localizer.Do($"{reputationTransferData.ReputationReceiver.UILink()} is a company legal person and can't receive reputation."),
                        reputationTransferData.ReputationSender,
                        NotificationCategory.Reputation,
                        NotificationStyle.InfoBox
                    );

                    return;
                }
            }

            if (CompaniesPlugin.Obj.Config.DenyCompanyMembersExternalReputationEnabled && receiverEmployeer != null && reputationTransferData.TargetType == ReputationTargetType.ReputationGivenToUser)
            {
                lawPostResult.Success = false;
                NotificationManager.ServerMessageToPlayer(
                    Localizer.Do($"You can't give reputation to a member of a company."),
                    reputationTransferData.ReputationSender,
                    NotificationCategory.Reputation,
                    NotificationStyle.InfoBox
                );

                return;
            }

            if (CompaniesPlugin.Obj.Config.DenyCompanyMembersReputationEnabled && reputationTransferData.TargetType == ReputationTargetType.ReputationGivenToUser) // we do not allow the employees to reputate internal, if the settings match
            {
                if (senderCompany != null)
                {
                    if (senderCompany.IsEmployee(reputationTransferData.ReputationReceiver) || senderCompany.InviteList.Contains(reputationTransferData.ReputationReceiver))
                    {
                        lawPostResult.Success = false;
                        NotificationManager.ServerMessageToPlayer(
                            Localizer.Do($"{reputationTransferData.ReputationReceiver.UILink()} is (or invited to become) a member of {senderCompany.UILink()} and can't receive any reputation from you."),
                            reputationTransferData.ReputationSender,
                            NotificationCategory.Reputation,
                            NotificationStyle.InfoBox
                        );

                        return;
                    }
                }

                if (receiverEmployeer != null)
                {
                    if (Company.IsInvited(reputationTransferData.ReputationSender, receiverEmployeer))
                    {
                        lawPostResult.Success = false;
                        NotificationManager.ServerMessageToPlayer(
                            Localizer.Do($"You are invited to become a member of {receiverEmployeer.UILink()} and can't give reputation to anyone in your company."),
                            reputationTransferData.ReputationSender,
                            NotificationCategory.Reputation,
                            NotificationStyle.InfoBox
                        );

                        return;
                    }
                }
            }

            if (lawPostResult.Success && reputationTransferData.TargetType == ReputationTargetType.ReputationGivenToUser && senderCompany != receiverEmployeer) // update both sides if we had success
            {
                Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t =>
                {
                    senderCompany?.UpdateLegalPersonReputation();
                    receiverEmployeer?.UpdateLegalPersonReputation();
                });
            }
        }

        public void InterceptStartHomesteadGameAction(StartHomestead startHomestead, ref PostResult lawPostResult)
        {
            if (!CompaniesPlugin.Obj.Config.PropertyLimitsEnabled) { return; }

            if (startHomestead.Citizen == null) { return; }

            // Check if they're employed
            var employer = Company.GetEmployer(startHomestead.Citizen);
            if (employer == null) { return; }

            // If the company currently has a HQ, block it
            if (employer.HasHQDeed)
            {
                lawPostResult.MergeFailLoc($"Can't start a homestead when you're an employee of a company with a HQ");
                Logger.Debug($"Preventing '{startHomestead.Citizen.Name}' from placing a homestead as their employer '{employer.Name}' already has a HQ '{employer.HQDeed.Name}'");
                return;
            }

            // This deed will become the company HQ
            lawPostResult.AddPostEffect(() =>
            {
                ClaimHomesteadAsHQAsyncRetry(startHomestead.Citizen, employer);
            });
        }

        public void InterceptPlaceOrPickupObjectGameAction(PlaceOrPickUpObject placeOrPickUpObject, ref PostResult lawPostResult)
        {
            if (!CompaniesPlugin.Obj.Config.PropertyLimitsEnabled) { return; }

            if (placeOrPickUpObject.Citizen == null) { return; }

            var isClaimDeed = placeOrPickUpObject.ItemUsed is SettlementClaimStakeItem settlementClaimStake || placeOrPickUpObject.ItemUsed is HomesteadClaimStakeItem homeClaimStake;

            // After any pickup, try and fixup homestead claim items
            if (placeOrPickUpObject.PlacedOrPickedUp == PlacedOrPickedUp.PickingUpObject)
            {
                // workaround V11 bug ECO-36228 which let's empty deeds behind... | save the deed before it's gone
                var deed = PropertyManager.GetDeedWorldPos(placeOrPickUpObject.ActionLocation.XZ);

                lawPostResult.AddPostEffect(() =>
                {
                    Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t => FixupHomesteadClaimItems(placeOrPickUpObject.Citizen));

                    // workaround V11 bug ECO-36228 which let's empty deeds behind... | remove the deed after a short delay to let the game catch up
                    if (deed != null && isClaimDeed)
                    {
                        Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t =>
                        {
                            try
                            {
                                if (deed.OwnedObjects?.Any() != true)
                                {
                                    // Logger.Debug($"Fixed up empty deed '{deed.Id}' left due to ECO-36228");
                                    deed.ForceChangeOwners(placeOrPickUpObject.Citizen, OwnerChangeType.CivicUpdate);
                                    Registrars.Get<Deed>().Remove(deed);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"Could not fix '{deed.Id}' left due to ECO-36228:");
                                Logger.Debug(ex.Message);
                            }
                        });
                    }
                });
            }
            else
            {
                var company = Company.GetEmployer(placeOrPickUpObject.Citizen);
                if (company != null)
                {
                    lawPostResult.AddPostEffect(() =>
                    {
                        Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t => { 
                            company.UpdateAllVehicles(); 
                            company.UpdateAllAuthLists(); 
                        });  // take over vehicle if we got some new

                        if (isClaimDeed)
                        {
                            Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t => company.TakeClaim(placeOrPickUpObject.Citizen, placeOrPickUpObject.ActionLocation.XZ)); // take claimstake over if it is one (special handling in compare to vehicle)
                        }
                    });
                }
            }
        }

        public void HandleDeedDestroyed(Deed deed)
        {
            if (deed.Owner is not User owner) { return; }
            Company.GetFromLegalPerson(owner)?.OnNoLongerOwnerOfProperty(deed);
        }

        public void HandleDeedOwnerChanged(Deed deed)
        {
            if (deed.Owner is not User newOwner) { return; }
            var company = Company.GetFromLegalPerson(newOwner);

            // Note: we're assuming that we're not already an owner of the deed (e.g. change between aliases both containing the legal person)
            company?.OnNowOwnerOfProperty(deed);
        }

        private void FixupHomesteadClaimItems(User employee)
        {
            var company = Company.GetEmployer(employee);
            if (company == null) { return; }
            // Sweep their inv looking for HomesteadClaimStakeItem items with the "user" field set to the legal person and change it to point at them instead
            foreach (var stack in employee.Inventory.AllInventories.AllStacks())
            {
                if (stack.Item is not HomesteadClaimStakeItem homesteadClaimStakeItem) { continue; }
                if (!homesteadClaimStakeItem.IsUnique) { continue; }
                if (homesteadClaimStakeItem.User != company.LegalPerson) { continue; }
                homesteadClaimStakeItem.User = employee;
                Logger.Debug($"Fixed up '{stack}' (homestead claim stake) to be keyed to '{employee.Name}' instead of {company.LegalPerson.Name}' after HQ deed was lifted");
            }
        }

        private void ClaimHomesteadAsHQ(User employee, Company employer, bool allowAsyncRetry = true)
        {
            var deed = employee.HomesteadDeed;
            if (deed == null)
            {
                if (allowAsyncRetry)
                {
                    ClaimHomesteadAsHQAsyncRetry(employee, employer);
                    return;
                }
                Logger.Error($"ClaimHomesteadAsHQ failed as employee.HomesteadDeed was null");
                return;
            }
            ClaimHomesteadAsHQ(employee, deed, employer);
        }

        private void ClaimHomesteadAsHQAsyncRetry(User employee, Company employer)
        {
            Task.Delay(CompaniesPlugin.TaskDelay).ContinueWith(t => ClaimHomesteadAsHQ(employee, employer, false));
        }

        private void ClaimHomesteadAsHQ(User employee, Deed deed, Company employer)
        {
            if (deed.Owner == employer.LegalPerson) { return; }
            deed.ForceChangeOwners(employer.LegalPerson, OwnerChangeType.Normal);
            employee.HomesteadDeed = null;
        }

        internal static string GetLegalPersonName(string companyName)
            => $"{companyName} Legal Person";

        internal static string GetCompanyAccountName(string companyName)
            => $"{companyName} Company Account";

        internal static string GetCompanyCurrencyName(string companyName)
            => $"{companyName} Shares";
    }
}
