import { Button, FormField, Rows, Text, TextInput, Title } from "@canva/app-ui-kit";
import { getCurrentPageMetadata, getDesignToken } from "@canva/design";
import React from "react";
import * as styles from "styles/components.css";

const syncEndpoint = "http://127.0.0.1:17334/sync/page";
const pollIntervalMs = 500;

type SyncState = "idle" | "watching" | "synced" | "error";

const getCurrentDesignId = async (): Promise<string> => {
  const { token } = await getDesignToken();
  const payload = token.split(".")[1];
  if (payload === undefined) {
    throw new Error("Canva did not provide a readable design token.");
  }

  const base64 = payload.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(payload.length / 4) * 4, "=");
  const claims: unknown = JSON.parse(atob(base64));
  if (
    typeof claims !== "object" ||
    claims === null ||
    !("designId" in claims) ||
    typeof claims.designId !== "string" ||
    claims.designId.length === 0
  ) {
    throw new Error("Canva did not expose a design ID for this editor session.");
  }

  return claims.designId;
};

export const App = (): React.JSX.Element => {
  const [pairingCode, setPairingCode] = React.useState("");
  const [isSyncing, setIsSyncing] = React.useState(false);
  const [syncState, setSyncState] = React.useState<SyncState>("idle");
  const [status, setStatus] = React.useState("Enter the pairing code shown by CanvaStage, then start watching the selected editor page.");
  const lastReportedPage = React.useRef<string | undefined>(undefined);

  const reportCurrentPage = React.useCallback(async (): Promise<void> => {
    try {
      const [designId, page] = await Promise.all([getCurrentDesignId(), getCurrentPageMetadata()]);
      if (page.type !== "absolute" || page.id === undefined) {
        setSyncState("error");
        setStatus("This Canva page does not expose a stable page ID, so CanvaStage cannot sync it.");
        return;
      }

      const pageKey = `${designId}:${page.id}`;
      if (lastReportedPage.current === pageKey) {
        setSyncState("watching");
        setStatus("Watching the selected Canva editor page.");
        return;
      }

      const response = await fetch(syncEndpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pairingCode, designId, pageId: page.id }),
      });
      if (!response.ok) {
        throw new Error(await response.text());
      }

      lastReportedPage.current = pageKey;
      setSyncState("synced");
      setStatus("Current Canva editor page sent to CanvaStage.");
    } catch (error: unknown) {
      setSyncState("error");
      setStatus(
        error instanceof Error
          ? `CanvaStage's listener at 127.0.0.1:17334 was not reached: ${error.message}. The companion itself runs separately at localhost:8080.`
          : "CanvaStage's listener at 127.0.0.1:17334 was not reached. The companion itself runs separately at localhost:8080.",
      );
    }
  }, [pairingCode]);

  React.useEffect(() => {
    if (!isSyncing) {
      lastReportedPage.current = undefined;
      return undefined;
    }

    let isDisposed = false;
    let timerId: number | undefined;
    const watch = async (): Promise<void> => {
      await reportCurrentPage();
      if (!isDisposed) {
        timerId = window.setTimeout(watch, pollIntervalMs);
      }
    };

    void watch();
    return () => {
      isDisposed = true;
      if (timerId !== undefined) {
        window.clearTimeout(timerId);
      }
    };
  }, [isSyncing, reportCurrentPage]);

  const startSync = (): void => {
    if (pairingCode.trim().length !== 8) {
      setSyncState("error");
      setStatus("Enter the eight-character pairing code from CanvaStage.");
      return;
    }

    lastReportedPage.current = undefined;
    setSyncState("watching");
    setStatus("Connecting to the local CanvaStage listener…");
    setIsSyncing(true);
  };

  const stopSync = (): void => {
    setIsSyncing(false);
    setSyncState("idle");
    setStatus("Watching stopped. CanvaStage remains on its last selected page.");
  };

  return (
    <div className={styles.scrollContainer}>
      <Rows spacing="2u">
        <Title size="small">CanvaStage Sync Spike</Title>
        <Text>
          This local test sends only the current Canva design ID and page ID to CanvaStage on this computer.
        </Text>
        <FormField
          label="CanvaStage pairing code"
          value={pairingCode}
          control={(props) => (
            <TextInput
              {...props}
              onChange={(value: string) => setPairingCode(value.trim().toUpperCase())}
              placeholder="Example: 1A2B3C4D"
            />
          )}
        />
        <Button variant="primary" onClick={startSync} disabled={isSyncing} stretch>
          Start watching page
        </Button>
        <Button variant="secondary" onClick={stopSync} disabled={!isSyncing} stretch>
          Stop watching
        </Button>
        <Text tone={syncState === "error" ? "critical" : "tertiary"}>{status}</Text>
        <Text tone="tertiary">
          This spike watches the selected Canva editor page every 500 ms. It does not access presentation text, credentials, or browser traffic.
        </Text>
      </Rows>
    </div>
  );
};
