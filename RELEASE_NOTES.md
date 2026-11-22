Inventory Ledger for ACT — experimental development build.

Implemented: ACT network subscription, reference packet decoders for inventory/retainer/FC snapshots, local persistence, cross-owner search, list/icon views, CSV export, observed gil balance changes, four-language UI, optional support and verified stable-release update preparation.

Important limitations:
- Live capture has NOT been verified against a running game. Capture is disabled by default and requires explicit approval of the current game build against the bundled Global 7.56h profile.
- Retainer ownership association follows observed retainer spawn names within the current character's known retainers; this path needs real-session validation.
- Own retainer sale history, confirmed player trades, and categorization of gil spending are NOT implemented. Market-wide sale history is deliberately not presented as your own sales.
- No glam dresser/armoire collection, automatic sorting mirror, or Chinese/Korean game-region decoder is included. UI supports English/Japanese/Chinese/Korean; item names use Japanese or English from XIVAPI.
- No complete recovery of unobserved/offline history is possible.

This prerelease is for validation, not a completed production release. See README for setup, supported packet profile, data storage and remaining validation.
