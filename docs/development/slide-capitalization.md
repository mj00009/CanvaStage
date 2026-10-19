# Match slide capitalization

This workaround is included in v0.14.1. Matching public help is published after the tag's Windows checks and screenshot review pass.

The affected PPTX supplied for #47 contains uppercase characters directly, while the editor screenshot shows sentence case. Re-download cannot recover information missing from the PPTX. The exact cause inside Canva remains unknown.

## Operator flow

In **Settings & Diagnostics → Monitor text capitalization**, enable **Match slide capitalization**. It is off by default. Use **Apply**, **Re-download**, or **Start setup** to check a deck that has not been processed yet. Applying it can take longer while Windows reads candidate slide images. No PDF export or external OCR service is used.

The status reports pages adjusted, candidate pages kept as exported, and pages that could not be checked. Review Monitor against Canva before service use. Turning the setting off restores the original exported text immediately, including current and next cues, without stopping or unblanking output. It affects text output and previews; the Mirror image is unchanged.

Original and matched text are stored separately in the local cache, along with the processing summary. The saved preference controls which is shown after restart. Older caches remain usable; Simple Setup imports an unchecked deck once when matching is enabled, then can reuse it normally. A missing recognizer is recorded as checked-but-unavailable; after fixing Windows OCR support, use Re-download to retry.

## Matching rules

Windows OCR reads the already-downloaded slide image locally. Only paragraphs with at least three consecutive uppercase words are candidates. A word needs at least two letters to participate in that run. Isolated acronyms, short headings and existing mixed-case names stay as exported.

The matcher first looks for the complete paragraph's word sequence, ignoring case. If another part of a long paragraph contains an OCR error, it can instead verify the entire uppercase run with up to four context words on each side, requiring at least eight words and 32 token characters in the matching window. Only the uppercase run receives casing changes. At the start/end of a paragraph, the available context is used. Conflicting occurrences cause that match to be rejected.

Word order, numbers, token boundaries and token lengths must match. Wording, whitespace, punctuation and line breaks always come from the PPTX. There is no sentence-case dictionary, guessed proper-name capitalization, cross-slide borrowing, or AI rewrite. This is a conservative workaround: it can leave bad capitalization unchanged when the evidence is insufficient. Exact word agreement does not guarantee that OCR read every capital correctly, so review the result.

The full-paragraph path also changes only uppercase runs, retaining mixed-case words and isolated acronyms even when OCR suggests different casing for them. A proper name inside an eligible uppercase run takes its casing from the matched image text.

## Windows and privacy

The app targets the Windows 10.0.19041 SDK to call `Windows.Media.Ocr` directly while retaining the Windows 11 x64 product requirement. No new third-party NuGet package, PDF library, OCR model download, PowerShell helper, or remote recognition endpoint was added. Windows must have a supported recognition language for the user's profile. Missing support, recognition failures, oversize images and timeouts retain exported text and increment the unavailable count. User cancellation still cancels the import.

Recognition is sequential during import, off the UI thread, with a 15-second per-page deadline and bounded image dimensions. It adds no Canva export requests and runs only on pages with candidate text. Progress identifies the recognition stage. Logs include counts and exception types, never recognized words or slide content.

## Verification

Neutral unit fixtures cover matching and mismatched wording, numbers, incomplete text, token boundaries, conflicting occurrences, isolated acronyms/mixed-case names, context matching around an unrelated OCR error, cache round trips, off-by-default behavior, next cue/title consistency and Simple Setup reuse.

Windows published-app checks exercise the real import path with injected recognition, disabled behavior, hidden-slide mapping, unavailable/failed recognition and cancellation. They also test cache restart, live/blank preservation while toggling, preference persistence, settings bindings and a real native OCR pass on neutral generated text when a Windows language is available. Review normal/narrow settings screenshots.

A private local probe using the supplied screenshot and PPTX restored the affected sentence-case prefix while retaining an unrelated OCR mismatch unchanged. The private files and their text were not copied into repository fixtures or public help. This does not yet prove recognition on Canva's actual compressed JPEG export of that slide; test Apply/Re-download with the feature enabled on the presentation computer before declaring #47 resolved.
