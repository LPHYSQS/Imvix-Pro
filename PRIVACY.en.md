# Privacy Policy for Imvix Pro

**Last Updated: April 29, 2026**
**Applicable Product: Imvix Pro (Pro Edition)**

## 1. Core Statement

Imvix Pro is a professional desktop application designed for advanced workflows. Its source code is publicly available and distributed under a custom license with commercial-use restrictions; no third party may use the project commercially without separate prior written authorization from the author or copyright holder.

Importantly, **public source availability does not mean cloud-based processing**. Imvix Pro follows a **local-first** processing model: image conversion, PDF/PSD/GIF handling, OCR, QR code and barcode recognition, local AI enhancement, AI background removal, and AI erase/inpainting features are, by default, performed locally on your device. The software does not rely on uploading your files to developer-operated servers or third-party cloud services for processing.

Unless you explicitly open an external link, contact us by email or other means, or your operating system or browser initiates a connection as a direct result of your action, Imvix Pro does not proactively transmit your file contents, processing results, or local configuration data to developer servers or third parties.

## 2. Scope of This Policy

This Privacy Policy explains how Imvix Pro handles privacy-related data during operation, including:

- whether the software collects or uploads user data
- what data may be stored locally on your device
- when external network activity may occur
- what control you have over locally stored data

This Policy does not apply to information you voluntarily submit through your browser, email provider, app store, or other third-party platforms. Such information is governed by the privacy policies of the relevant third-party services.

## 3. Data We Do Not Collect or Upload by Default

During normal use of Imvix Pro, we do **not** automatically collect, upload, sell, rent, or share the following through the software:

- the contents of your source files, images, documents, or processed outputs
- OCR-recognized text, QR code contents, or barcode contents
- personal identity information such as your name, phone number, address, or identification details
- account information, because Imvix Pro does not require an account for its core functionality
- device identifiers, advertising identifiers, IP addresses, or location data
- usage telemetry, behavioral analytics, ad tracking data, or profiling data
- automatically submitted remote crash reports or diagnostic copies

In other words, Imvix Pro is not built around cloud analytics, remote inference, or background telemetry as a requirement for normal operation.

## 4. Data That May Be Stored Locally on Your Device

To provide preference persistence, history, failure troubleshooting, and temporary processing support, Imvix Pro may store the following data locally on your device:

| Local path | What it may contain | Primary purpose |
| --- | --- | --- |
| `%AppData%\Imvix Pro\settings.json` | language, theme, default output rules, presets, preview tool parameters, watched-folder configuration, window state, and similar settings | stores your application preferences and workflow configuration |
| `%AppData%\Imvix Pro\history.json` | recent conversion history (currently up to the latest 12 entries) | allows you to review recent task results |
| `%AppData%\Imvix Pro\Logs\conversion-*.log` | failure timestamps, output parameters, selected file paths, and failure reasons | local troubleshooting when batch tasks fail |
| `%LocalAppData%\ImvixPro\Logs\*.log` | local runtime logs, warning messages, exception stacks, and some file paths or state information | local diagnostics and runtime troubleshooting |
| `%TEMP%\ImvixPro\...`, `%TEMP%\Imvix_Gif_*`, and similar temporary directories | intermediate files, exported temporary frames, AI working directories, temporary unlocked files, or rendered results | supports local processing and is typically cleaned up after tasks complete |

The data listed above remains stored locally on your device by default and is not automatically uploaded to external servers as part of Imvix Pro's normal processing flow.

If an older `%AppData%\Imvix` directory exists on your device, the software may attempt to migrate it locally to `%AppData%\Imvix Pro`. That migration also occurs entirely on your device.

## 5. How Files and Content Are Handled

Imvix Pro only accesses local content when you actively select files, drag and drop files, import folders, or enable a locally configured watched folder.

In those scenarios:

- input files are chosen by you, and the software does not proactively scan unrelated personal directories for remote upload purposes
- output files are written only to the output directory you specify or to the applicable local directory defined by your current rules
- image, document, OCR, QR code, barcode, and AI-related processing is performed locally
- local temporary files may be created to complete GIF, PDF, AI, or similar workflows, and the software will attempt to clean them up after the task finishes
- your original files, output files, and processing results are not retained on developer-operated servers

## 6. Network Connectivity, External Links, and Connectivity Boundaries

Imvix Pro's core processing capabilities are **not designed to require a continuous internet connection**. Core conversion, recognition, and local AI features can run in a local environment.

In the following limited scenarios, an external connection may occur only because of your direct action:

- you click an external link such as the official website, and your system opens the page in your default browser
- you click a QR code or barcode URL recognized inside the application, and your system may open that link in your default browser
- you voluntarily contact the developer by email, website form, or another external channel

These network actions are not background uploads of processing data by Imvix Pro. They occur only after your explicit action and are then handled by your operating system, browser, or the relevant third-party site. Any such site processes your access data under its own privacy policy.

## 7. Third-Party Components and Local Runtimes

Imvix Pro may include third-party libraries, models, and runtimes that execute locally to provide image processing, PDF/PSD rendering, OCR, QR code and barcode recognition, and local AI functionality. Their inclusion does not, by itself, mean that your data is remotely transmitted.

Imvix Pro also does not include third-party advertising SDKs, analytics SDKs, or automated user-profiling trackers as part of its core product behavior.

## 8. Data Sharing and External Disclosure

Because Imvix Pro does not receive your file contents or processing outputs by default, we do not maintain a normal business process of selling, renting, or sharing your pending files, output files, or recognition results with third parties as part of the software's standard operation.

If disclosure is required by applicable law, or if you voluntarily submit information to us through an external channel, the relevant handling will depend on the legal requirement or the specific circumstances in which you submitted that information.

## 9. Your Control Over Local Data

Because the relevant data is stored primarily on your own device, you can generally exercise direct control over it, including by:

- viewing or backing up the local configuration and history files under `%AppData%\Imvix Pro`
- deleting `%AppData%\Imvix Pro\history.json` to clear recent history
- deleting log files under `%AppData%\Imvix Pro\Logs\` and `%LocalAppData%\ImvixPro\Logs\`
- clearing Imvix Pro temporary files from your system temporary directories
- uninstalling the software and manually deleting remaining local data directories

Please note that deleting these files may permanently remove your presets, watched-folder configuration, history, or parts of your local diagnostic records.

## 10. Security Notice

Imvix Pro's local-processing architecture helps reduce exposure associated with transmitting files over the internet. However, the security of locally stored data still depends on the security of your own device. We recommend that you:

- protect your operating system account with a strong password and appropriate access controls
- keep your operating system and security patches up to date
- use extra caution with history files, logs, and outputs on shared devices
- make local backups of important files and configuration data when appropriate

## 11. Policy Updates

We may update this Privacy Policy from time to time to reflect product changes, compliance requirements, or documentation maintenance. The latest version will be identified by the "Last Updated" date at the top of this document.

## 12. Contact Information

If you have questions about this Privacy Policy, or would like to discuss commercial licensing, product compliance, or privacy boundaries, you may contact us through:

- Email: `339106817@qq.com`
- Official website: [https://lphysqs.github.io/ImvixWeb/](https://lphysqs.github.io/ImvixWeb/)

---

**Summary: Imvix Pro publicly provides its source code under a custom license with commercial-use restrictions. Its key data-processing features run locally by default, do not depend on cloud processing, and do not include built-in background telemetry uploads.**
