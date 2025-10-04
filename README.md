# Eco Companies

A server mod for Eco 12.0 that extends the law and economy system with player controllable companies.

## Installation

1. Download `EcoCompaniesMod.dll`
2. Copy the `EcoCompaniesMod.dll` file to `Mods` folder of the dedicated server.
3. Restart the server.

## Compatibility

There is no direct dependency or special support for the following mods but their features should be compatible:

* [EcoCivicsImportExportMod](https://github.com/thomasfn/EcoCivicsImportExportMod) - importing/exporting laws using game values added by this mod
* [EcoSmartTaxMod](https://github.com/thomasfn/EcoSmartTaxMod) - using Smart Tax/Pay/Rebate actions on the company legal person

  * accessing the company's tax card is a bit awkward but doable via `/tax othercard <company name> Legal Person`

## Usage

Companies are created and managed through a set of chat commands. All related commands are sub-commands of `/company`.

### Overview

A company can be created at any time by any player using `/company create <name>`.

### Managing Employees

A company has one CEO and zero to many employees. The CEO is implicitly considered an employee also. The CEO can invite other players to the company using `/company invite <playername>`, and they must accept the invitation to join using `/company join <companyname>`. A player can only be in one company at a time, though they can leave at any time using `/company leave`. The CEO can also fire employees using `/company fire <playername>`.

### Company Bank Account

Every company has a legal person and a bank account created for it. The name of the legal person will be `XXX Legal Person` and the name of the bank account will be `XXX Company Account`, where `XXX` is the name of the company. The legal person is a fake user of sorts and is seen by the citizen timer law trigger. All employees are given user rights to the company bank account but not manager rights, meaning that company wealth is not included as part of their wealth. The legal person is the sole manager of the account and considered to have 100% of the wealth of the company. The user list of the bank account is automatically updated whenever someone joins or leaves the company.

### Company Property

Every company can own property. This is achieved by a player passed ownership of a deed to the company's legal person. Like bank accounts, all employees will be automatically authed, but also invited as residents. Due to technical limitations of game, employees will not be able to directly edit the deed after doing this (e.g. claim more plots, unclaim existing plots or remove the whole deed). Instead they must change ownership of the deed back to themself, make the edit and then pass it back to the legal person.

### Company Citizenship

The company has a settlement citizenship, which is tied to that of the legal person. this can be managed using the `/company citizenship` command. Any citizenship changes affecting the legal person will be propagated to the company.

* Use `/company citizenship apply,<settlementname>` to apply for the company to join a settlement
* Use `/company citizenship join,<settlementname>` to accept an invitation for the company to join a settlement
* Use `/company citizenship leave` to have the company leave the current settlement

The company's ability to apply, join and leave the settlement may be subject to restrictions depending on how the server and the settlements in question are configured. Generally you will need to remove the HQ deed when trying to change company citizenship. The company's citizenship may also change if the HQ deed is annexed by a different settlement.

### Property Limits Mode

As of 10.0 the mod includes a property limits mode, enabled by default, that prevents employees of a company from having homestead claims. Instead, the company itself gets a HQ homestead claim.

* When founding a company, if the founder already has a homestead claim, it will automatically be transferred and become the company HQ
* If the company does not have a HQ and an employee tries to place a homestead claim, it will automatically be transferred and become the company HQ
* If the company has a HQ, employees will be prevented from placing homestead claims
* Players can't join a company if they have a homestead claim, even if the company does not yet have a HQ - they must remove their homestead claim first
* The HQ maximum plot limit will be multiplied for every employee. For example, if the default max plots for a homestead claim is 12, a 3-person company's HQ will have a max plot count of 36 (see below for a new 12.0+ option)
* Any employee can claim or unclaim plots for the company HQ by selecting their claim tool and running `/company claim`, which will set the claim tool to the HQ deed
* The citizenship of all employees is bound to that of the company and is updated automatically whenever the company legal person citizenship has changed

As of 120 the mod includes new options configure how base claims are calculated, see the table below.

| Setting Name | Type | Default | Description |
| - | - | - | - |
| PropertyLimitsEnabled | Boolean | On | If enabled, employees may not have homestead deeds, and the company gets a HQ homestead deed that grows based on employee count. |
| UseBaseClaimsPerEmployee | Boolean | Off | Enabled the configured base claims from `BaseClaimPerEmployee` instead of the server setting. |
| BaseClaimsPerEmployee | SortedDictionary | (Formula) | This will override the base claims for the company hq (employee count => amount of claims per employee) when property limits are enabled and the option above is enabled. Exmaple in the config template. |


### Reputation

As of 11.0 the mod includes several settings to customise the way reputation within companies is handled. For now, the default settings will not change any behaviour, e.g. as if the feature didn't exist.

| Setting Name | Type | Default | Description |
| - | - | - | - |
| DenyLegalPersonReputationEnabled | Boolean | Off | If enabled, the legal person of a company can't receive reputation (this does not include the 'ReputationAverages'). |
| DenyCompanyMembersExternalReputationEnabled | Boolean | Off | If enabled, the company members can't receive reputation. |
| DenyCompanyMembersReputationEnabled | Boolean | Off | If enabled, the company members can't give reputation to each other nor the legal person (also counts for invited members). |
| ReputationAveragesEnabled | Boolean | Off | If enabled, the average repuation from all employees will be given to the legal person (in addition to their own reputation if they have any). |
| ReputationAveragesBonusEnabled | Boolean | On | If enabled, the average repuation from all employees will be filtered by known bonussources (currently only SpeaksWellOfOthersBonus). |

### Vehicles

As of 11.0 the mod includes some settings to enable the placement of vehicles being automatically transferred to the legal person. For now, the default settings are off.

| Setting Name | Type | Default | Description |
| - | - | - | - |
| VehicleTransfersEnabled | Boolean | Off | If enabled, the company vehicles will be adopted to the legal person on placement (need `PropertyLimitsEnabled` to be also enabled). |
| VehicleTransfersUseCompanyNameEnabled | Boolean | On | If enabled, the company name instead of the legal persons name will be used for naming (shorter). |

### Legislation

The mod also extends the law system with a number of game values and triggers to assist with writing company-aware laws.

#### Game Values

The following game values allow your laws to query company related information.

##### Account Legal Person

Retrieves the legal person user from a given bank account. This is helpful to derive the subject company, if any, for law triggers that involve a currency transaction, e.g. "Currency Transfer".

| Property Name | Type | Description |
| - | - | - |
| BankAccount | Bank Account | The company bank account used to resolve the owner company. |

##### Employer Legal Person

Retrieves the legal person user from a given employee user. This is helpful to derive the employer company, if any, for law triggers that involve a citizen - for example, placing blocks, cutting trees or claiming property.

| Property Name | Type | Description |
| - | - | - |
| Citizen | User | The employee. |

##### Company CEO

Retrieves the CEO user from a given company. The legal person for the company will be needed as context.

| Property Name | Type | Description |
| - | - | - |
| LegalPerson | User | The legal person of the company being evaluated. This could be retrieved from context via Account Legal Person or Employer Legal Person, or directly if using a citizen timer combined with a Is Company Legal Person condition. |

##### Employee Count

Retrieves the number of employees of a company, including the CEO. The legal person for the company will be needed as context.

| Property Name | Type | Description |
| - | - | - |
| LegalPerson | User | The legal person of the company being evaluated. This could be retrieved from context via Account Legal Person or Employer Legal Person, or directly if using a citizen timer combined with a Is Company Legal Person condition. |

##### Skill Count

Retrieves the number of specialisations of all employees of a company, including Self Improvement. This only counts skills into which a star has been invested. There is an option to choose unique skills only or not and an option to choose whether to pick the highest skill count or sum them. The legal person for the company will be needed as context.

| Property Name | Type | Description |
| - | - | - |
| LegalPerson | User | The legal person of the company being evaluated. This could be retrieved from context via Account Legal Person or Employer Legal Person, or directly if using a citizen timer combined with a Is Company Legal Person condition. |
| UniqueSkills | Yes/No | Whether to consider unique skills only. For example, two employees both with Mining would count as 2 skills, but only 1 unique skill. |
| Highest | Yes/No | Whether to select the highest number of skills held per employee rather than the sum. |

##### Is CEO Of Company

Gets if the given citizen is the CEO of any company.

| Property Name | Type | Description |
| - | - | - |
| Citizen | User | The citizen being checked. |

##### Is Employee Of Company

Gets if the given citizen is the employee (or CEO) of any company.

| Property Name | Type | Description |
| - | - | - |
| Citizen | User | The citizen being checked. |

##### Is Company Legal Person

Gets if the given citizen is the generated legal person user for a company.

| Property Name | Type | Description |
| - | - | - |
| Citizen | User | The citizen being checked. |

#### Triggers

The following triggers will help your laws respond to certain company related events.

##### Company Expense

Triggered when money is credited to a company account. If the recipient is also a company account, a separate 'Company Income' trigger will occur in addition to this one.

| Property Name | Type | Description |
| - | - | - |
| Source Bank Account | Bank Account | The account from which the funds were transferred. |
| Target Bank Account | Bank Account | The account to which the funds were transferred. |
| Currency | Currency | The type of currency that was transferred. |
| Currency Amount | Number | The amount of currency that was transferred. |
| Receiver Legal Person | Citizen | The legal person of the company who received the funds. |

##### Company Income

Triggered when money is debited from a company account.

| Property Name | Type | Description |
| - | - | - |
| Citizen | Citizen | The citizen responsible for initiating the transfer. |
| Source Bank Account | Bank Account | The account from which the funds were transferred. |
| Target Bank Account | Bank Account | The account to which the funds were transferred. |
| Currency | Currency | The type of currency that was transferred. |
| Currency Amount | Number | The amount of currency that was transferred. |
| Sender Legal Person | Citizen | The legal person of the company who sent the funds. |

##### Citizen Join Company

Triggered when a citizen is attempting to accept an invite to join a company. Can be prevented.

| Property Name | Type | Description |
| - | - | - |
| Citizen | Citizen | The citizen who is joining the company. |
| Company Legal Person | Citizen | The legal person of the company. |

##### Citizen Leave Company

Triggered when a citizen is attempting to leave a company, either of their own accord or by being fired. Can be prevented.

| Property Name | Type | Description |
| - | - | - |
| Citizen | Citizen | The citizen who is leaving the company. |
| Company Legal Person | Citizen | The legal person of the company. |
| Fired | Boolean | If the person is leaving due to being fired. |

##### Private Property Ban

As of 12.0 this event is triggered whenever a private property ban to a company account is done. Thie can't prevented.

| Property Name | Type | Description |
| - | - | - |
| Citizen | Citizen | The citizen who's money was pulled. |
| Company Legal Person | Citizen | The legal person of the company. |
| CurrencyAmount | Float | The amount of money that was pulled. |
| Currency | Currency | The currency that was pulled. |
| TaxCode | String | The taxcode. Can be set within the mods config. |


## License

[MIT](https://choosealicense.com/licenses/mit/)

