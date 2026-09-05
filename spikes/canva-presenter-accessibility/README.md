# Canva Presenter accessibility spike

This spike tests whether Windows exposes Canva Presenter View's current page number to CanvaStage through its built-in accessibility system. It does not capture the screen, read slide text, or inspect browser traffic.

## Before testing

- Use the Windows computer that runs CanvaStage during a service.
- Apply the same presentation in CanvaStage that you will present in Canva. The page totals must match before CanvaStage will follow a detected page.
- Open Canva in the Windows desktop app, Chrome, Microsoft Edge, Firefox, Brave, or Opera.
- Do not run one app as administrator and the other normally. Both apps should run at the same permission level.

## Run the test

1. Start the spike build of CanvaStage.
2. In **Presentation**, confirm that the correct Canva presentation is applied.
3. In Canva, open that presentation and start the presenter mode normally used during a service.
4. Return to CanvaStage and open **Setup & diagnostics**.
5. Find **Canva Presenter accessibility spike** and select **Start presenter scan**.
6. Return to Canva Presenter View and move forward and backward through several pages.
7. Check whether CanvaStage's selected page follows the presenter.

The scan remains off until you start it. Manual page controls continue to work while it is stopped and if the scan cannot find a page.

## What the result means

- **Synced to presenter page…** — the spike found a usable page counter and CanvaStage followed it.
- **The presenter reports … pages, but the applied presentation has …** — apply the matching Canva presentation in CanvaStage and retry.
- **No Canva presenter window was found** — make sure Presenter View is open on the same computer. If it is, record which Canva/browser app you used.
- **A possible Canva presenter window was found, but Windows did not expose a page counter** — the presenter UI is not publishing the information this approach needs. The next test should use image matching.
- **Windows blocked accessibility access** — close both apps, reopen both normally, and retry. Avoid running only one of them as administrator.

## Feedback to record

Please report:

- Canva web or Windows desktop app;
- browser name, if applicable;
- standard Present, Presenter View, or another Canva presentation mode;
- the exact status shown under **Canva Presenter accessibility spike**;
- whether CanvaStage followed forward and backward page changes.

Do not include Canva credentials, authorization codes, or tokens in the report.
