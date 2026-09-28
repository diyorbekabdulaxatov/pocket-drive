# Asset licence register

Every third-party asset in the project, where it came from and whether it may ship in a paid game.
Update this file whenever an asset is added or removed.

**Rule (agreed with Diyorbek, 28 September 2026):** non-commercial assets are allowed as placeholders during development. Every row marked **NC — replace before release** must be swapped for a commercially usable asset before any public or paid release.

| Asset | Path | Author / source | Licence | Commercial use | Notes |
|---|---|---|---|---|---|
| Porsche 911 Carrera 4S (adapted) | `Assets/PocketDrive/Vehicles/Porsche911/` | Karol Miklas, Sketchfab | CC BY-SA 4.0 | Yes, with credit and share-alike for the model | Real brand: debadge and rename or replace before a paid release (trademark). |
| asphalt_02, concrete_pavement, concrete_floor_worn_001, plastered_wall_04, red_brick_03 | `Assets/PocketDrive/Textures/` | Poly Haven | CC0 | Yes | No credit required. |
| City | `Assets/PocketDrive/Imported/City/` | "City" by Mateusz Woliński, https://sketchfab.com/3d-models/city-1f50f0d6ec5a493d8e91d7db1106b324 | CC BY 4.0 | Yes, with credit | Downtown map. Includes static parked vehicles. |
| Parking lot | `Assets/PocketDrive/Imported/ParkingLot/` | "Parking lot" by Veterock, https://sketchfab.com/3d-models/parking-lot-80e54d8326ea4646949961e8ada35518 | CC BY 4.0 | Yes, with credit | Parking challenge venue in Downtown. |
| Lotus Exige 240 | `Assets/PocketDrive/Imported/LotusExige/` | "AC - Lotus Exige 240 [FREE]" by David & 3D, https://sketchfab.com/3d-models/ac-lotus-exige-240-free-a770a08d0c17446cbec7721f3dd1ede7 | Sketchfab Standard | Yes | Player car in Downtown. Real brand: trademark question before a paid release, as with the Porsche. |
| glTFast importer | `Packages/manifest.json` | Unity Technologies | Unity Companion License | Yes | Package, not art. |

Removed: Quaternius placeholder pedestrians (CC0) — removed 28 September 2026 at Diyorbek's request (cartoon style).

## Licence quick guide

- **CC0 / public domain:** free for anything, no credit needed.
- **CC BY:** commercial use allowed; credit the author in the game's credits.
- **CC BY-SA:** as CC BY, and changes to the asset itself must be shared under the same licence.
- **Sketchfab "Free Standard":** commercial use allowed inside a game; the raw asset cannot be resold or redistributed.
- **Unity Asset Store (free):** Standard Unity Asset Store EULA; commercial use in a game allowed.
- **CC BY-NC (NonCommercial):** placeholder only — NC — replace before release.
- **CC BY-ND (NoDerivatives):** cannot be modified; avoid.
