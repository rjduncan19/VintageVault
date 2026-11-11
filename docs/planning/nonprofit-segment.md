# Segment: Churches, Nonprofits, and Charities

_Created: 2026-11-11 | Source: [design chat handoff](2026-11-design-chat-handoff.md), section 3.4_

## Why this segment

Small congregations and charities share the target-user profile from [product-strategy.md](product-strategy.md): important records (books, donor records, member lists), no IT staff, and little or no recovery plan. They also tend to rely on part-time staff and volunteers using personal devices. [MISSION.md](../../MISSION.md) already commits to free backup for nonprofits; this doc turns that line into a segment.

## Motivating case (Oct 2026)

A medium-sized, tech-forward and progressive congregation in the author's region had **two separate compromises in one year**. It is a good example because it already relies on technology for accessibility (Zoom, simulcast services and so on), so its exposure is larger than its size suggests.
1. A part-time bookkeeper's personal device, also used for other clients, was hacked.
2. Separately, the minister's email account was compromised.

Both were used to phish the whole membership. The congregation is still unsure whether it is fully clean.

## Honest framing

**VintageVault would not have prevented either compromise.** Both were account or device takeovers, which backup doesn't stop. VV's value here is:
- **Recoverability:** the books, donor records and member lists can be restored.
- **Known-good snapshots from before an incident:** the organization can see what its data looked like before the compromise and restore from there. When "are we clean?" can't be answered, this matters a lot.

Marketing must say this plainly ([Principle 3](../../MISSION.md#3-protect-dont-profit-from-fear): no fear-selling).

## Pair backup with remedial security education

The compromises above are mostly fixed by education, not software:
- MFA on every account, and phishing-resistant sign-in (passkeys) where possible.
- Separating contractor devices from organization accounts. This is the bring-your-own-device (BYOD) bookkeeper problem.
- Recognizing phishing that comes from a trusted sender's real, compromised account.

VintageVault can offer short, plain-language guides alongside the product (MISSION.md "Giving Back" #4), aimed at staff, volunteers *and* members.

## Two-way referrals

Point people to existing guidance and earn links back:
- CISA guidance for faith-based organizations
- Global Cyber Alliance small-organization toolkit
- TechSoup and NTEN
- Denominational bodies and regional associations

Open source and a free tier make VV easy for these groups to recommend. **A pilot case study would help most.**

## Distribution

TechSoup and denominational networks each reach many congregations at once. One endorsement there is worth more than direct outreach.

## Related project: Harbor

[Harbor](https://github.com/rjduncan19/Harbor) (elder device management) overlaps on the BYOD-contractor problem: a personal device used for an organization's accounts. Guidance and tooling for that problem could be shared between the two projects.

## Next steps

- [ ] Ask the motivating congregation whether it would pilot (not yet asked).
- [ ] Draft the first plain-language guide (MFA + phishing from compromised accounts).
- [ ] Check each referral organization's current guidance before linking to it.
- [ ] Decide whether the nonprofit offer is the free tier as-is or Pro at no cost (depends on the open revenue question, [ADR-005 D5](../architecture/005-credential-gating-and-isolation.md#d5-revenue-model-open-question-not-decided)).
