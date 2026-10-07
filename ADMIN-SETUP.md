# Firebase admin account directory

The admin dashboard lists Firebase Authentication accounts and their sign-in providers. Passwords are never returned by Firebase Authentication's user-list API and are not shown in the dashboard.

## One-time setup

1. In Firebase Console, enable the sign-in providers required by the shop and create the admin account in **Authentication → Users**.
2. Install the Firebase CLI, authenticate with an account that can deploy Cloud Functions, then deploy from the repository root:

   ```powershell
   firebase login
   firebase deploy --only functions
   ```

   Cloud Functions deployment may require the Firebase project's billing plan to support Functions.
3. In a trusted local environment, authenticate Application Default Credentials with a project administrator. Install function dependencies and grant the admin claim to the existing Firebase user:

   ```powershell
   gcloud auth application-default login
   $env:GOOGLE_CLOUD_PROJECT = "web-ban-hoa-9fe0b"
   npm install --prefix functions
   $env:ADMIN_EMAIL = "admin@example.com"
   npm --prefix functions run set-admin
   Remove-Item Env:ADMIN_EMAIL
   Remove-Item Env:GOOGLE_CLOUD_PROJECT
   ```

   Do not commit service-account keys or credentials. The script preserves any existing custom claims.
4. Sign out and sign back in with that account. Serve the site over HTTP(S), and add the site's hostname under **Authentication → Settings → Authorized domains**.

## Admin access

The admin page and callable function both require the Firebase ID token's `admin: true` custom claim. The function returns only UID, email, display name, provider IDs, creation/last-sign-in times, and disabled status. It uses 100-user pages; choose **Tải thêm** to fetch further accounts.
