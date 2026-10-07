# Admin account directory

The admin dashboard lists Firebase Authentication accounts and their sign-in providers. Passwords are never returned by Firebase Authentication's user-list API and are not shown. The website uses a Vercel Function with the Firebase Admin SDK; it does not require Firebase Cloud Functions or a Firebase Blaze upgrade.

## One-time setup

1. In Firebase Console, enable the sign-in providers required by the shop and create the admin account in **Authentication → Users**.
2. Add the Vercel production hostname under **Authentication → Settings → Authorized domains**.
3. In Google Cloud Console for the same project, create a service account with the **Firebase Authentication Admin** role. Create a JSON key and store it only as a Vercel environment variable:

   - Open Vercel → project `flower-com` → **Settings → Environment Variables**.
   - Add `FIREBASE_SERVICE_ACCOUNT_JSON` with the entire contents of the service-account JSON file. Select **Production** (and Preview too if preview deployments should have access).
   - Add `FIREBASE_PROJECT_ID` with `web-ban-hoa-9fe0b`.
   - Never paste the key into source code, commit it, or send it in chat. Restrict/delete the key if it is accidentally exposed.

4. Redeploy the Vercel project after saving those variables. Its Root Directory must remain `flower.com`; the API is at `/api/admin/users`.
5. Grant the admin claim to the existing Firebase user from a trusted local terminal. Install Google Cloud CLI if needed, then authenticate Application Default Credentials as a project administrator:

   ```powershell
   gcloud auth application-default login
   npm install
   $env:ADMIN_EMAIL = "admin@example.com"
   npm run set-admin
   Remove-Item Env:ADMIN_EMAIL
   ```

   Run these commands from the `flower.com` directory. The script preserves existing custom claims and does not require downloading a service-account key.
6. Sign out and sign back in with that account so Firebase refreshes its ID token, then open `/admin.html` and select **Tài khoản**.

## Admin access

The admin page and Vercel API both require the Firebase ID token's `admin: true` custom claim. The API verifies the token server-side and returns only UID, email, display name, provider IDs, creation/last-sign-in times, and disabled status. It uses 100-user pages; choose **Tải thêm** to fetch further accounts.
