# Emberforge Security Information

## Why can antivirus software detect Emberforge?

Emberforge is a standalone trainer and modding tool for Enshrouded.

To provide its features, Emberforge connects to the running Enshrouded process and performs operations such as:

- Reading process memory
- Writing process memory
- Searching for game data and memory signatures
- Modifying runtime values
- Installing runtime hooks for certain features

These techniques are commonly used by legitimate trainers, debuggers and modding tools.

However, similar techniques can also be used by malicious software.

Because of this, antivirus products may classify Emberforge using generic labels such as:

- Trainer
- GameHack
- HackTool
- Riskware
- Suspicious
- Generic
- Heuristic

A detection alone does not necessarily prove that Emberforge is malicious, but users should always make their own security decisions.

---

## Source Code Availability

The Emberforge source code is publicly available in this repository for:

- Transparency
- Community review
- Nexus Mods security review
- Debugging
- Future development

Where possible, released source-code versions should correspond to the same Emberforge version distributed through Nexus Mods.

---

## Official Downloads

For your own security, download Emberforge only from official project locations.

Modified builds distributed by third parties may contain changes that are not present in the official Emberforge source code.

---

## Save Game Safety

Emberforge modifies game behavior while Enshrouded is running.

Although the tool is designed to restore or disable modifications when possible, unexpected behavior can never be completely ruled out.

Users should regularly back up:

- Enshrouded save files
- Worlds
- Important characters

This is especially recommended before using experimental functionality or after major Enshrouded updates.

---

## Reporting Security Issues

If you discover behavior in Emberforge that appears unsafe or unintended, please report it through the project's GitHub or Nexus Mods page.

Please include:

- Emberforge version
- Enshrouded version
- The affected feature
- A description of what happened
- Antivirus detection name, if applicable
