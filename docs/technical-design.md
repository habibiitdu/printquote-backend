# PrintQuote: Technical Design (v1)

PrintQuote produces draft quotations for an offset printing press. The person quoting picks a product template, fills in the job details, and the system prices the job from configured rate tables. The cost sheet it produces has the same layout as the press's current Excel sheets.

The product contains no AI or LLM features. Every price is a deterministic calculation over configured rates.

| | |
|---|---|
| Backend repository | `printquote-backend` (ASP.NET Core, .NET 9) |
| Frontend repository | `printquote-frontend` (React 19 + TypeScript + Vite) |
| Database | PostgreSQL 15+ |
| Status | v1, first build |

---

## 1. Scope of v1

In scope:

1. **Rate masters**: papers, presses with print rate slabs, finishing operations, binding types with slabs, profit rules, global settings.
2. **Product templates**: components, and for each field whether the form asks for it, defaults it or locks it.
3. **Customers and companies**: companies are the letterheads (Swift Print, Quality Park Limited).
4. **Costing engine**: calculates a cost sheet from a job specification and a rate book.
5. **Quotations**: create, list, view, re-price as a new version, change status. Each version stores its inputs, its result and a snapshot of the rates used.
6. **Quantity breaks**: the same job priced at several quantities.
7. **Two document views**: the internal cost sheet and the customer quotation, both printable from the browser.

Out of scope for v1, planned later: authentication and roles, PDF/Excel export on the server, job tickets after approval, multi-tenant hosting, digital-press click pricing, gripper margins.

---

## 2. Printing terms used in this document

| Term | Meaning in this system |
|---|---|
| **Parent sheet** | The sheet as bought from the paper supplier. Default 23" × 36". |
| **Rim** | 500 parent sheets. Paper bought by the rim is priced per rim. |
| **Demy** | Press sheet of 18" × 23", which is half a parent sheet (2 per parent sheet). |
| **Double Demy (DD)** | Press sheet of 23" × 36", a whole parent sheet. |
| **Forma** | One Demy sheet printed on both sides. At 8.5" × 11" that is 8 pages. A 0.5 forma cover is 4 pages. |
| **Plate** | One plate per colour per printing form. Priced per press. |
| **Work-and-turn** | Both sides of a form are on one plate. The sheet is printed, turned and printed again, then cut in two. All forma-measured components are printed this way. |
| **Forma per plate** | How much forma (both sides) one plate carries on a press. Demy = 0.5, Double Demy = 1. |
| **Ups** | How many flat pieces fit on one press sheet. |
| **Impression** | One press sheet passing through the press once with one plate. |
| **Make-ready** | Sheets wasted setting up each plate. Configured per press, in press sheets per plate. |

---

## 3. Architecture

```
┌──────────────────────┐     HTTPS / JSON      ┌───────────────────────────────┐
│  printquote-frontend │ ───────────────────▶  │  PrintQuote.Api               │
│  React + Vite SPA    │   /api/*              │  Controllers, ProblemDetails  │
└──────────────────────┘                       └──────────────┬────────────────┘
                                                              │
                                    ┌─────────────────────────┴───────────────┐
                                    ▼                                         ▼
                     ┌───────────────────────────────┐       ┌──────────────────────────────┐
                     │  PrintQuote.Application       │       │  PrintQuote.Infrastructure   │
                     │  Costing engine (pure),       │◀──────│  EF Core + Npgsql DbContext, │
                     │  services, DTOs, validation   │       │  migrations, seed data       │
                     └───────────────┬───────────────┘       └──────────────┬───────────────┘
                                     ▼                                      ▼
                     ┌───────────────────────────────┐              ┌──────────────┐
                     │  PrintQuote.Domain            │              │  PostgreSQL  │
                     │  Entities, enums              │              └──────────────┘
                     └───────────────────────────────┘
```

Rules:

- **The costing engine is pure.** It takes a `JobSpec` and a `RateBook` and returns a `CostSheet`. It never touches the database, the clock or HTTP. This makes it easy to unit test against the press's sample sheets.
- **Services load rates, call the engine and save results.** A quotation version stores the `RateBook` snapshot it was priced with, as `jsonb`. Changing a rate later never changes a saved quotation.
- **Money is `decimal`** in C# and `numeric` in PostgreSQL. It is never a float.
- **Enums travel as camelCase strings** in JSON.

### 3.1 Backend solution layout

```
printquote-backend/
├── PrintQuote.sln
├── docs/technical-design.md
├── docker-compose.yml                 # optional local PostgreSQL
├── src/
│   ├── PrintQuote.Domain/             # entities, enums
│   ├── PrintQuote.Application/
│   │   ├── Costing/                   # CostingEngine, JobSpec, RateBook, CostSheet
│   │   ├── Dtos/                      # request/response records
│   │   └── Services/                  # quotation + master-data services
│   ├── PrintQuote.Infrastructure/
│   │   ├── Persistence/               # AppDbContext, configurations, seed
│   │   └── Migrations/
│   └── PrintQuote.Api/                # Program.cs, controllers
└── tests/
    └── PrintQuote.Application.Tests/  # xUnit, engine tests from sample sheets
```

### 3.2 Frontend layout

```
printquote-frontend/
├── src/
│   ├── api/            # typed fetch client + types mirroring the API contract
│   ├── components/     # layout, tables, form controls
│   ├── pages/          # Dashboard, NewQuotation, QuotationDetail, settings pages
│   ├── lib/            # formatting (en-IN numbers), helpers
│   └── styles/         # design tokens + global CSS
└── vite.config.ts      # dev proxy /api → http://localhost:5080
```

Libraries: `react-router-dom` (routing), `@tanstack/react-query` (server state). No UI kit; styling is plain CSS with tokens matching the approved mockup.

---

## 4. Costing engine

### 4.1 Inputs

```
JobSpec
  quantity                 int > 0
  pageWidthIn, pageHeightIn  decimal?   finished page size; needed for forma lamination area
  components[]             ComponentSpec
  bindingTypeId            guid?      null = no binding
  extras[]                 { finishingOperationId, amount? }   job-level lines, e.g. cutting & packing
  commissionPercent, overheadPercent, marketingPercent   decimal (default 0)
  profitPercentOverride    decimal?
  customerId, templateId   guid?      used only to resolve the profit %

ComponentSpec
  name                     "Cover", "Inner", "Map"…
  measure                  forma | ups
  paperId                  guid
  pressId                  guid
  -- forma measure --
  forma                    decimal > 0, multiple of 0.125
  colors                   int 1..4   (same colours both sides, work-and-turn)
  halfFormaPressId         guid?      if set, the fractional part of forma prints on this press
  countsTowardBindingForma bool       true for inner pages, false for covers
  -- ups measure --
  openWidthIn, openHeightIn  decimal
  colorsFront, colorsBack    int 0..4 (front ≥ 1)
  finishing[]              { finishingOperationId, sides? }   sides used by per-square-inch operations
```

`RateBook` holds every paper, press (with slabs), finishing operation, binding type (with slabs), profit rule, customer margin, template margin and the settings in force when the job is priced.

### 4.2 Algorithm

Rounding helpers:

- `ceilTo(x, step)` = round x **up** to a multiple of `step`, with a tolerance of 1e-9.
- Money rounding is `Math.Round(x, 2, MidpointRounding.AwayFromZero)`, applied to each line total. Totals are sums of rounded lines.

**Per component, forma measure** (press `R`, optional half-forma press `H`):

1. Split the forma into parts. If `H` is set, `F ≥ 1` and `F` has a fraction: the whole part `⌊F⌋` goes on `R` and the fraction goes on `H`. Otherwise all of `F` goes on `R`.
2. For each part `(Fp, press X)`:
   - `platesPerColour = ceil(Fp / X.formaPerPlate)`
   - `plates = platesPerColour × colors`
   - `netSheets = Fp × qty / (2 × X.formaPerPlate)` (work-and-turn gives two copies per sheet)
   - `pressSheets = ceil(netSheets + plates × X.makeReadySheetsPerPlate)`
   - `impressions = pressSheets × 2 × colors`
   - `parentSheets += pressSheets / X.piecesPerParentSheet`
   - add a **print line** for the part (4.3)

**Per component, ups measure** (press `X`):

1. `ups = max(⌊W/w⌋·⌊H/h⌋, ⌊W/h⌋·⌊H/w⌋)`, where `W×H` is the press sheet and `w×h` the open size. If `ups = 0`, the spec is rejected with a validation error on that component's `pressId`.
2. `plates = colorsFront + colorsBack`
3. `pressSheets = ceil(qty / ups) + plates × X.makeReadySheetsPerPlate`
4. `impressions = pressSheets × plates` (one pass per plate)
5. `parentSheets = pressSheets / X.piecesPerParentSheet`
6. Add a print line (4.3).

**Paper line, per component:**

- Paper sold by the **rim**: `units = ceilTo(parentSheets / 500, settings.rimRoundingStep)` (default step 0.05).
- Paper sold by the **sheet**: `units = ceil(parentSheets)`.
- `total = units × paper.pricePerUnit`.

**Plate lines:** plates are summed per press, giving one line per press: `"<Press> plate"`, `qty × press.platePrice`.

**Finishing per component**, for each selected operation:

| Basis | Quantity | Total |
|---|---|---|
| perSquareInch | area × qty × sides. Area = `F × 4 × pageW × pageH` (forma) or `w × h` (ups) | qty × rate |
| perThousand | qty / 1000 | qty × rate |
| perPiece | qty | qty × rate |
| fixed | 1 | rate |

**Binding:** `bindingForma = Σ forma` of components with `countsTowardBindingForma`. The rate is the first slab where `upToForma` is null or `bindingForma ≤ upToForma`. Total = `qty × rate`.

**Job extras:** same bases as finishing. For `fixed`, `amount` (if given) replaces the configured rate.

**Totals:**

```
rawMaterialsTotal = Σ plate lines + Σ paper lines
conversionTotal   = Σ print + finishing + binding + extras lines
productionCost    = rawMaterialsTotal + conversionTotal
commission        = productionCost × commissionPercent / 100   (likewise overhead, marketing)
totalCost         = productionCost + commission + overhead + marketing
profit            = totalCost × profitPercent / 100
totalBill         = totalCost + profit
ratePerPiece      = round(totalBill / qty, 2)
```

**Profit % resolution**, first match wins:

1. `profitPercentOverride` on the job
2. the customer's `profitPercent`
3. the template's `profitPercent`
4. profit rules by ascending `priority`, the first where `maxQuantity` is null or `qty ≤ maxQuantity`
5. otherwise 0, with a warning

The cost sheet reports which source won (`profitSource`).

### 4.3 Print charge

For a print line with `plates` and `impressions` on press `X`:

1. `perPlate = impressions / plates`
2. Walk `X.rateSlabs` ordered by `upToImpressionsPerPlate` ascending, null last. Take the first slab where `upTo` is null or `perPlate ≤ upTo`.
3. Slab `flatPerPlate`: the line is `plates` × `rate`, unit `plate`.
4. Slab `perThousand`: the line is `impressions / 1000` × `rate`, unit `thousand`.

Seeded slabs:

| Press | Up to impressions per plate | Charge |
|---|---|---|
| Demy | 2,000 | 300 flat per plate |
| Demy | 3,000 | 350 flat per plate |
| Demy | 6,000 | 450 flat per plate |
| Demy | over | 150 per 1,000 |
| Double Demy | 5,000 | 600 flat per plate |
| Double Demy | 20,000 | 200 per 1,000 |
| Double Demy | over | 180 per 1,000 |

### 4.4 Cost sheet output

```
CostSheet
  quantity
  descriptionLines[]   e.g. "Cover: 0.5 forma, 70 gsm Offset, 2 colour"
  lines[]              { sl, category: rawMaterial|conversion, description, quantity, unit, rate, total, component }
  rawMaterialsTotal, conversionTotal, productionCost
  commission, overhead, marketing, totalCost
  profitPercent, profitSource, profit, totalBill, ratePerPiece
  trace[]              plain-language steps, e.g. "Inner: 5 forma on Double Demy …"
  warnings[]
```

Line order: plate lines (per press), paper lines (per component), print lines (per component part), finishing (per component), binding, extras.

### 4.5 Worked example (verified in unit tests)

English (2): 25,000 pcs at 8.5" × 11". Cover 0.5 forma on 70 gsm, 2 colours, Demy. Inner 5 forma on 55 gsm, 1 colour, Double Demy. Pin binding. Profit rule 10% over 5,000.

| Line | Working | Total |
|---|---|---|
| Demy plate | ceil(0.5/0.5) × 2 colours = 2 × 180 | 360.00 |
| Double Demy plate | ceil(5/1) × 1 = 5 × 370 | 1,850.00 |
| 70 gsm (Cover) | 12,500 + 2×25 = 12,550 Demy → 6,275 parent → 12.55 rim × 2,850 | 35,767.50 |
| 55 gsm (Inner) | 62,500 + 5×20 = 62,600 DD → 125.2 rim × 2,250 | 281,700.00 |
| Print Demy (Cover) | 12,550 × 2 × 2 = 50,200 imp; 25,100 per plate → 150/1,000 | 7,530.00 |
| Print DD (Inner) | 62,600 × 2 × 1 = 125,200 imp; 25,040 per plate → 180/1,000 | 22,536.00 |
| Pin binding | 5 forma → 0.90 × 25,000 | 22,500.00 |
| **Production** | | **372,243.50** |
| Profit 10% | | 37,224.35 |
| **Total bill** | | **409,467.85** |
| **Rate per piece** | | **16.38** |

This matches the press's sheet: 12.55 and 125.2 rims, 2 + 5 plates, 50.2k and 125.2k impressions.

### 4.6 Known differences from the sample sheets

The sample sheets add wastage by hand, and the amounts vary (for example 0.2 to 0.8 rim on similar jobs). The engine applies one rule (make-ready sheets per plate). Some samples therefore differ by a few hundredths of a rim. Errors found in the samples and **not** reproduced:

- Lecture 5 forma, 25,000: Print DD 225.5 thousand (should be about 112.7)
- OMR-A / OMR-C: a stray "Print DD 250.8 thousand" line
- The 0.125 / 0.25 / 0.5 / 1 forma sheets are unfinished copies with identical numbers
- Lecture 5.5 forma, 2,500: print charged 150/200 per plate instead of 350/600

---

## 5. Data model (PostgreSQL)

Every table has `id uuid PK`, `created_at timestamptz` and `updated_at timestamptz`. Names are snake_case.

| Table | Key columns |
|---|---|
| `companies` | name, address, phone, is_active |
| `customers` | name, address, phone, email, profit_percent numeric?, is_active |
| `papers` | name, paper_type, gsm int, sheet_width_in, sheet_height_in, unit (rim/sheet), price_per_unit numeric, effective_from date, is_active |
| `presses` | name, sheet_width_in, sheet_height_in, pieces_per_parent_sheet int, plate_price, make_ready_sheets_per_plate int, forma_per_plate numeric, is_active |
| `press_rate_slabs` | press_id FK, up_to_impressions_per_plate int?, charge_type (flatPerPlate/perThousand), rate |
| `finishing_operations` | name, basis (perSquareInch/perThousand/perPiece/fixed), rate, is_active |
| `binding_types` | name, is_active |
| `binding_rate_slabs` | binding_type_id FK, up_to_forma numeric?, rate_per_piece |
| `profit_rules` | name, priority int, max_quantity int?, profit_percent, is_active |
| `app_settings` | single row: rim_rounding_step, default_commission_percent, default_overhead_percent, default_marketing_percent |
| `product_templates` | name, description, default_page_width_in?, default_page_height_in?, default_binding_type_id?, profit_percent?, is_active |
| `template_components` | template_id FK, name, measure, sort_order, counts_toward_binding_forma, default_finishing jsonb |
| `template_fields` | component_id FK, field_key, behaviour (ask/default/locked), default_value text?, allowed_values text?, min numeric?, max numeric?, step numeric? |
| `template_extras` | template_id FK, finishing_operation_id FK, default_amount numeric? |
| `quotations` | number text unique (Q-0001…), job_name, customer_id FK, company_id FK, template_id FK?, status, current_version int, quantity, total_bill, rate_per_piece |
| `quotation_versions` | quotation_id FK, version_number, note, spec jsonb, cost_sheet jsonb, rate_snapshot jsonb, created_at |

The quotation number comes from a PostgreSQL sequence. `quotations` repeats the current version's quantity and totals so the list screen needs no JSON parsing.

Template field keys: `forma`, `colors`, `paperId`, `pressId`, `halfFormaPressId`, `openWidthIn`, `openHeightIn`, `colorsFront`, `colorsBack`.

---

## 6. API contract (v1)

Base URL `/api`. JSON uses camelCase. Errors are RFC 7807 `ProblemDetails`, and validation errors carry an `errors` map (`field → messages[]`). IDs are GUID strings. Dates are ISO 8601.

### 6.1 Master data

All master-data resources support `GET /x`, `GET /x/{id}`, `POST /x` (returns 201), `PUT /x/{id}` and `DELETE /x/{id}` (returns 204). Delete deactivates the row (`isActive=false`), so it no longer prices new jobs but anything that refers to it stays readable. Profit rules are the exception and are removed. `GET /x` returns inactive rows too, so the settings screens can reactivate them.

Slab lists (press rate slabs, binding slabs) must contain exactly one open-ended slab (`upTo…` null) and no duplicate limits.

| Resource | Body (POST/PUT; GET returns the same plus `id`) |
|---|---|
| `/companies` | `{ name, address, phone, isActive }` |
| `/customers` | `{ name, address, phone, email, profitPercent?, isActive }` |
| `/papers` | `{ name, paperType, gsm, sheetWidthIn, sheetHeightIn, unit, pricePerUnit, effectiveFrom, isActive }`. GET adds `rimWeightKg` and `pricePerKg` (computed). |
| `/presses` | `{ name, sheetWidthIn, sheetHeightIn, piecesPerParentSheet, platePrice, makeReadySheetsPerPlate, formaPerPlate, isActive, rateSlabs: [{ upToImpressionsPerPlate?, chargeType, rate }] }` |
| `/finishing-operations` | `{ name, basis, rate, isActive }` |
| `/binding-types` | `{ name, isActive, slabs: [{ upToForma?, ratePerPiece }] }` |
| `/profit-rules` | `{ name, priority, maxQuantity?, profitPercent, isActive }` |
| `/templates` | `{ name, description, defaultPageWidthIn?, defaultPageHeightIn?, defaultBindingTypeId?, profitPercent?, isActive, components: [{ name, measure, sortOrder, countsTowardBindingForma, defaultFinishing: [{ finishingOperationId, sides? }], fields: [{ fieldKey, behaviour, defaultValue?, allowedValues?, min?, max?, step? }] }], extras: [{ finishingOperationId, defaultAmount? }] }` |

Settings: `GET /settings` and `PUT /settings` with `{ rimRoundingStep, defaultCommissionPercent, defaultOverheadPercent, defaultMarketingPercent }`.

Child collections (slabs, components, fields, extras) are replaced as a whole on PUT.

### 6.2 Costing

`POST /costing/calculate`

```json
{ "spec": { ...JobSpec }, "breakQuantities": [1000, 2500, 10000] }
```

Response:

```json
{
  "costSheet": { ...CostSheet },
  "breaks": [ { "quantity": 1000, "profitPercent": 20, "totalBill": 23973.00, "ratePerPiece": 23.97 } ]
}
```

Returns `400` when the spec is invalid (unknown paper, quantity ≤ 0, ups component that does not fit…).

### 6.3 Quotations

| Method & path | Body | Returns |
|---|---|---|
| `GET /quotations?status=&search=&page=1&pageSize=20` | | `{ items: QuotationSummary[], total, page, pageSize }` |
| `GET /quotations/{id}` | | `QuotationDetail` |
| `POST /quotations` | `{ jobName, customerId, companyId, templateId?, spec, note? }` | `QuotationDetail` (version 1) |
| `POST /quotations/{id}/versions` | `{ jobName?, spec, note? }` | `QuotationDetail` (new current version) |
| `PUT /quotations/{id}/status` | `{ status }` (draft/sent/approved/lost) | `QuotationDetail` |
| `GET /quotations/{id}/breaks?quantities=1000,2500` | | breaks priced with the **current version's rate snapshot** |

```
QuotationSummary { id, number, jobName, customerId, customerName, companyId, companyName,
                   templateId?, templateName?, quantity, totalBill, ratePerPiece, status,
                   currentVersion, updatedAt }
QuotationDetail  = QuotationSummary + { versions: [{ versionNumber, note, createdAt, spec, costSheet }] }
                   (versions ordered newest first)
```

The rate snapshot is stored but not returned by default, to keep payloads small.

---

## 7. Frontend screens (v1)

| Route | Screen |
|---|---|
| `/` | Dashboard: status counts, template shortcuts, quotation list with status filter and search |
| `/quotations/new?template={id}` | New quotation. Job (letterhead, customer, job name, template, quantity, page size). Components built from the template (locked fields hidden, defaults pre-filled, ask fields required). Binding, extras, overheads & profit. Live cost sheet (debounced `POST /costing/calculate`), quantity breaks and calculation trace. Save creates the quotation. |
| `/quotations/{id}` | Quotation detail. Internal cost sheet / customer quotation toggle in the Excel layout, quantity breaks, version history, status actions, Edit (loads the spec into the form and saves a new version), browser print. |
| `/settings/papers` | Paper table and edit form, with price per kg ↔ per rim helper |
| `/settings/presses` | Presses with rate slabs |
| `/settings/finishing` | Finishing operations, binding types with slabs, profit rules, settings |
| `/settings/templates` | Template list and editor |
| `/settings/customers`, `/settings/companies` | Simple tables with forms |

Numbers display in Indian grouping (`en-IN`, e.g. 4,09,467.85), with 2 decimals for money.

---

## 8. Seed data

When `Database:MigrateOnStartup` is on and the database has no presses, the API seeds the following from the sample sheets (`DbSeeder`). Seeded papers, presses, finishing operations and binding types have fixed IDs (`10000000-…0055` for 55 gsm, `20000000-…0001` for Demy and so on), so example requests and tests can refer to them.

- Companies: Swift Print, Quality Park Limited. Customer: Panacea Publications (Kawran Bazar).
- Papers (23" × 36"): 55 gsm Offset (Partex) 2,250/rim; 70 gsm Offset 2,850/rim; 100 gsm Offset (Partex) 4,000/rim; 120 gsm Art Paper 4,700/rim; 300 gsm Art Card 15/sheet.
- Presses: Demy (18×23, 2 per parent, plate 180, make-ready 25, 0.5 forma/plate) and Double Demy (23×36, 1 per parent, plate 370, make-ready 20, 1 forma/plate), with the slabs in 4.3.
- Finishing: Glue lamination 0.004/sq in; Cover crease 150/1,000; Cutting & packing (fixed); Packing (fixed); Delivery & others (fixed).
- Binding: Pin (slabs ≤2: 0.7, ≤4.5: 0.8, ≤5: 0.9, ≤6.5: 1.0, ≤7: 1.2, ≤9: 1.3, over: 1.5); Glue 0.8.
- Profit rules: ≤5,000 → 20% (priority 1); over → 10% (priority 2).
- Templates: Lecture sheet / book, Single sheet (OMR), Map book, Memory test / class test.

---

## 9. Local development

Prerequisites: .NET SDK 9, Node 22+, PostgreSQL 15+ (local service or `docker compose up -d`), and `dotnet-ef` 9.

```bash
# Backend
cd printquote-backend
dotnet user-secrets --project src/PrintQuote.Api set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=printquote;Username=postgres;Password=<your password>"
dotnet ef database update --project src/PrintQuote.Infrastructure --startup-project src/PrintQuote.Api
dotnet run --project src/PrintQuote.Api        # http://localhost:5080, Swagger at /swagger
dotnet test

# Frontend
cd printquote-frontend
npm install
npm run dev                                     # http://localhost:5173, proxies /api to :5080
```

When running in Development, the API applies pending migrations and seeds an empty database on startup. Expected errors (400 validation, 404 not found) are returned as ProblemDetails by an MVC filter and are not logged as errors. Anything else is logged and returns a 500 ProblemDetails.

---

## 10. Conventions

- **Branches:** `main` is always releasable. Work happens on `feature/<short-name>` branches merged by pull request.
- **Commits:** imperative subject line, under 72 characters.
- **Backend:** nullable reference types on, warnings as errors in `Domain` and `Application`, file-scoped namespaces, records for DTOs, async all the way, no business logic in controllers.
- **Frontend:** TypeScript strict mode. API types live only in `src/api/types.ts`. Server state goes through React Query, never copied into local state except form drafts.
- **Tests:** every engine rule gets a unit test. A change to a formula must update section 4 of this document in the same pull request.

---

## 11. Open questions for the press

1. Make-ready waste per plate on each press: the engine uses 25 Demy sheets and 20 DD sheets.
2. The exact short-run flat-charge thresholds (4.3).
3. Whether a leftover half forma goes on Demy or Double Demy by default. The samples do both. It is a per-component choice (`halfFormaPressId`).
4. The 300 gsm art card sheet size, which affects ups for covers.
5. How forma is counted for book sizes other than 8.5" × 11".
