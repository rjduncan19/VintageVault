# 02 — OneDrive Custom App Setup

> **Goal:** Register your own Azure application for rclone (and VintageVault) instead of relying on rclone's shared client credentials. This is the professional, production-grade approach.

---

## Why Not Use the Default Shared App?

When you leave `client_id` and `client_secret` blank during `rclone config`, rclone uses a **shared application** registered by the rclone team. This works, but has significant drawbacks:

```
┌────────────────────────────────────────────────────────────────┐
│              Shared App (rclone default)                       │
│                                                                │
│   All rclone users share the same:                             │
│   • API rate limit quota                                       │
│   • Microsoft Graph throttling budget                          │
│   • Verification status (Microsoft can revoke it)              │
│   • OAuth consent screen (shows "rclone" not your app name)   │
│                                                                │
│   You have NO control over:                                    │
│   • When it gets rate-limited                                   │
│   • Whether Microsoft revokes it for policy violation          │
│   • The permissions granted                                    │
└────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────┐
│              Your Own App (recommended)                        │
│                                                                │
│   You control:                                                 │
│   • Your own dedicated API quota                               │
│   • Exact permissions (principle of least privilege)           │
│   • The consent screen (shows YOUR app name)                   │
│   • Client secret rotation schedule                            │
│   • Which accounts can authorize it                            │
└────────────────────────────────────────────────────────────────┘
```

**VintageVault always uses its own registered app** — never a shared credential. This tutorial shows you how to do the same for rclone.

---

## Prerequisites

- A Microsoft account (personal Outlook/Hotmail, or work/school account)
- Access to [Azure Portal](https://portal.azure.com) — free Microsoft accounts can access this

> **For personal OneDrive (consumer accounts):** Use a personal Microsoft account (outlook.com, hotmail.com, live.com) when registering the app. Apps registered under work/school tenants behave differently for personal accounts.

---

## Step 1: Register the Application

### 1.1 Open Azure Portal

Go to [portal.azure.com](https://portal.azure.com) and sign in.

### 1.2 Navigate to App Registrations

In the top search bar, type **"App registrations"** and click the result.

```
Azure Portal
└── App registrations
    └── New registration  ← click this
```

### 1.3 Fill in Registration Details

| Field | Value | Notes |
|-------|-------|-------|
| **Name** | `rclone-onedrive` | Or `VintageVault-dev` for the VV app |
| **Supported account types** | "Personal Microsoft accounts only" | For consumer OneDrive |
| **Redirect URI** | Platform: `Public client/native` → `http://localhost` | Required for rclone's auth flow |

Click **Register**.

### 1.4 Copy Your Application (Client) ID

After registration, the Overview page shows:

```
Application (client) ID:  xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Directory (tenant) ID:    xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

**Copy the Application (client) ID.** You'll need this shortly.

---

## Step 2: Configure Authentication Settings

### 2.1 Enable Public Client Flows

In your app's left sidebar: **Authentication**

Under **Advanced settings**:
- **Allow public client flows** → Toggle to **Yes**
- Click **Save**

This allows rclone to authenticate using the device code flow and installed-app flow without a client secret.

### 2.2 Verify Redirect URIs

Under **Platform configurations**, you should see:

```
Mobile and desktop applications
└── Redirect URIs
    └── http://localhost   ← should be present
```

If it's missing, click **Add a platform** → **Mobile and desktop applications** → check `http://localhost` → **Configure**.

---

## Step 3: Set API Permissions

### 3.1 Open API Permissions

In the left sidebar: **API permissions**

You'll see `User.Read` is added by default (Microsoft Graph).

### 3.2 Add OneDrive Permissions

Click **Add a permission** → **Microsoft Graph** → **Delegated permissions**

Search for and add these permissions:

| Permission | Why |
|-----------|-----|
| `Files.ReadWrite` | Read and write files in OneDrive |
| `offline_access` | Get refresh tokens (stay logged in) |
| `User.Read` | Already present — read basic profile |

> **Principle of least privilege:** If you only need to read (backup source), use `Files.Read` instead of `Files.ReadWrite`. For a backup destination, you need `Files.ReadWrite`.

After adding, your permissions should look like:

```
Microsoft Graph (3 permissions)
├── Files.ReadWrite   Delegated  ✓
├── offline_access    Delegated  ✓
└── User.Read         Delegated  ✓
```

You do **NOT** need to click "Grant admin consent" for delegated permissions used with a personal account.

---

## Step 4: Create a Client Secret (Optional but Recommended)

A client secret provides extra authentication beyond just the app ID. It proves that the code connecting is genuinely your app.

> **Note:** For pure rclone CLI use, you can often skip this (rclone can auth with just the client ID using device code flow). For VintageVault's programmatic use, a client secret is strongly recommended.

### 4.1 Create the Secret

In the left sidebar: **Certificates & secrets** → **Client secrets** tab → **New client secret**

| Field | Value |
|-------|-------|
| **Description** | `rclone-primary` |
| **Expires** | 24 months (or custom) |

Click **Add**.

### 4.2 ⚠️ Copy the Secret Value NOW

```
┌──────────────────────────────────────────────────────────────┐
│  ⚠️  CRITICAL: Copy the secret Value immediately.            │
│                                                              │
│  After you leave this page, the full value is NEVER          │
│  shown again. Only the last 4 characters will be visible.    │
│                                                              │
│  Value:    abc123~XYZ789_secretvalue-here                    │
│                                                              │
│  Store it in your password manager NOW.                      │
└──────────────────────────────────────────────────────────────┘
```

---

## Step 5: Configure rclone with Your Custom App

Now re-run `rclone config` (or reconfigure the existing remote) using your new app credentials.

### 5.1 New Remote with Custom App

```powershell
rclone config
```

Choose `n` for new remote. When prompted:

```
name> onedrive-personal-myapp

Storage> onedrive

client_id> xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```
*(paste your Application client ID)*

```
client_secret> abc123~XYZ789_secretvalue-here
```
*(paste your client secret — or press Enter if you skipped Step 4)*

```
Edit advanced config?
y/n> n

Use web browser to automatically authenticate rclone with remote?
y/n> y
```

Your browser opens. Sign in to the Microsoft account you want to backup.

Complete the rest of the wizard as in Tutorial 01.

### 5.2 Update an Existing Remote

If you already have a remote configured without a custom app, edit it:

```powershell
rclone config
# Select: e (Edit existing remote)
# Select your remote by name
# Update client_id and client_secret fields
```

Or directly edit the config file:

```powershell
notepad "$env:APPDATA\rclone\rclone.conf"
```

Add `client_id` and `client_secret` to your remote's section:

```ini
[onedrive-personal]
type = onedrive
client_id = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
client_secret = abc123~XYZ789_secretvalue-here
token = {"access_token":"eyJ...","refresh_token":"0.AQIA...","expiry":"..."}
drive_id = b!abc123...
drive_type = personal
```

Then reconnect (get a fresh token with your new app):

```powershell
rclone config reconnect onedrive-personal:
```

---

## Step 6: Verify the Custom App is Being Used

### Check which app is authorizing

```powershell
rclone config show onedrive-personal
```

Output should show your `client_id`:
```
[onedrive-personal]
type = onedrive
client_id = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
client_secret = *** ENCRYPTED ***
drive_id = b!abc123...
drive_type = personal
token = *** ENCRYPTED ***
```

### Test access

```powershell
rclone about onedrive-personal:
```

If it works, your custom app is correctly authorized.

### Verify in Azure Portal

Go to **App registrations** → Your app → **Overview** → **Users and Groups** (for work accounts) or check **Sign-ins** in the Azure AD logs if you have access. For personal accounts, sign-ins aren't logged in the portal.

---

## Setting Up a Second OneDrive Account

For the cross-account backup scenario (Tutorial 04), you need **two remotes** — one for each OneDrive account. You can use the same Azure app for both, or register separate apps.

### Using the same app for two accounts

The same app can authorize multiple users. Just configure a second remote:

```powershell
rclone config
# n → new remote
# name: onedrive-backup (the account receiving backups)
# type: onedrive
# Same client_id and client_secret
# Authenticate with your SECOND Microsoft account in the browser
```

Your config will now have:

```ini
[onedrive-personal]
type = onedrive
client_id = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
client_secret = abc123~XYZ...
token = {...token for account 1...}
drive_id = b!drive1...

[onedrive-backup]
type = onedrive
client_id = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
client_secret = abc123~XYZ...
token = {...token for account 2...}
drive_id = b!drive2...
```

Each remote has its own token for its own account, but uses the same app registration.

---

## Securing the Client Secret

### Option 1: Environment Variables (Recommended for scripts)

Instead of storing the secret in the config file, use environment variables:

```powershell
$env:RCLONE_CONFIG_ONEDRIVE_PERSONAL_CLIENT_SECRET = "abc123~XYZ..."
rclone lsd onedrive-personal:
```

rclone reads `RCLONE_CONFIG_{REMOTE}_{KEY}` format (all uppercase, hyphens become underscores).

### Option 2: Encrypted Config

rclone can encrypt the entire config file with a password:

```powershell
rclone config
# Choose: s (Set configuration password)
# Enter a strong password
```

From now on, rclone will prompt for this password on startup (or you set `RCLONE_PASSWORD_COMMAND` to retrieve it from a password manager).

### Option 3: Rotate Secrets Regularly

Set a calendar reminder every 12 months to:
1. Generate a new client secret in Azure Portal
2. Update the rclone config
3. Delete the old secret from Azure Portal

---

## Troubleshooting App Registration Issues

### "AADSTS700016: Application not found"
The `client_id` doesn't match any registered app. Double-check you copied the **Application (client) ID**, not the **Directory (tenant) ID**.

### "AADSTS50011: The redirect URI doesn't match"
Ensure `http://localhost` is added as a redirect URI under **Mobile and desktop applications** (not Web).

### "AADSTS7000218: The request body must contain the following parameter: client_assertion or client_secret"
You need to enable **Allow public client flows** under Authentication settings.

### "AADSTS65005: Invalid scope"
Check that `Files.ReadWrite` and `offline_access` are added to API permissions.

---

## Summary: What You've Built

```
Azure Portal
└── App Registration: "rclone-onedrive"
    ├── Application ID: xxxxxxxx-...  ← your client_id in rclone.conf
    ├── Client Secret: abc123~...     ← your client_secret in rclone.conf
    ├── Redirect URI: http://localhost
    ├── Auth: Public client flows enabled
    └── Permissions: Files.ReadWrite, offline_access, User.Read

rclone.conf
├── [onedrive-personal]  ← your personal OneDrive (source)
│   └── uses your Azure app + token for account 1
└── [onedrive-backup]    ← backup destination account
    └── uses your Azure app + token for account 2
```

---

## Next Steps

Continue to → [03 — Backup OneDrive to External SSD](03-backup-onedrive-to-external-ssd.md)
