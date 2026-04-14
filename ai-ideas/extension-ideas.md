# Browser Extension Feature Ideas

Notes: Improvements to the existing extension under `extension/` focusing on capture, submission, and sharing UX.

- Automatic Score OCR & Autofill
  - Description: Use OCR to extract numeric scores from screenshots and prefill the submission form in the extension.
  - Impact: High (reduces friction). Effort: Medium.

- In-extension Quick Share
  - Description: Compose a lightweight post from the extension (screenshot + caption) and share to user's profile or social networks.
  - Impact: Medium. Effort: Small.

- One-click Capture + Crop + Submit Flow
  - Description: Allow capture, crop, annotate, and immediate submit with minimal clicks; optionally skip the full site flow.
  - Impact: Medium. Effort: Small.

Suggested next steps:
- Prototype OCR locally using Tesseract or a small JS OCR lib against a few example screenshots.
- Add an integration test that simulates the capture -> submit UX using the extension's scripts.
