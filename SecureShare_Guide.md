# Kotlin Learning Path: Building "SecureShare"

**Project:** SecureShare (Android File Encryption App)  
**Goal:** Build an app to select, encrypt, and share files from the ground up.  
**Language:** Kotlin  

---

## Phase 0: The Setup

Before writing code, we need to initialize the environment.

1.  **Open Android Studio**.
2.  **New Project** -> Select **"Empty Views Activity"**.
    * *Why?* This gives us the classic XML layout structure, which is easier to grasp for beginners than Jetpack Compose.
3.  **Name:** `SecureShare`
4.  **Language:** Kotlin
5.  **Min SDK:** API 24 (Android 7.0).

---

## Phase 1: The Blueprint (Layout/XML)

We need to define what the user sees. In Android, the UI is often written in XML (Extensible Markup Language).

**Task:**
Open `res/layout/activity_main.xml`. You need to construct a screen with:
1.  A parent container (`LinearLayout`) to stack items vertically.
2.  A `TextView` to show status messages.
3.  A `Button` to trigger Encryption.
4.  A `Button` to trigger Decryption.

**Concepts to research/implement:**
* `android:id`: This gives the element a variable name we can use in Kotlin.
* `android:layout_width="match_parent"`: Make the view as wide as the screen.
* `android:onClick`: (Note: We will actually handle clicks in Kotlin, which is cleaner than doing it in XML).

---

## Phase 2: The Engine (Cryptography Class)

We need a dedicated helper object to handle the heavy math. We will use the **AES** algorithm in **CBC** mode.

**Task:**
Create a new Kotlin file named `CryptoManager.kt`. 

**Step 2.1: Singleton Pattern**
Define it as an `object` rather than a `class`.
* *Concept:* In Kotlin, an `object` is a Singleton—there is only ever one instance of it. You don't need to "construct" it.

**Step 2.2: The Constants**
Define your algorithm settings using `const val`.
* Algorithm: `KeyProperties.KEY_ALGORITHM_AES`
* Block Mode: `KeyProperties.BLOCK_MODE_CBC`
* Padding: `KeyProperties.ENCRYPTION_PADDING_PKCS7`

**Step 2.3: Key Management**
Write a function `getKey()` that accesses the `AndroidKeyStore`.
* *Logic:* Check if a key named "secret_key" exists. If yes, return it. If no, use `KeyGenerator` to create a new secure key.

**Step 2.4: Encryption Logic**
Write a function `encrypt(inputStream: InputStream, outputStream: OutputStream)`.
* *Logic:*
    1. Initialize a `Cipher` object in `ENCRYPT_MODE`.
    2. Write the IV (Initialization Vector) to the top of the output file.
    3. Read the input file in chunks (buffers) and feed them into the Cipher.
    4. Write the resulting encrypted bytes to the output stream.

**Step 2.5: Decryption Logic**
Write a function `decrypt(inputStream: InputStream, outputStream: OutputStream)`.
* *Logic:* Almost identical to encryption, but you must read the IV from the top of the file *first* to initialize the Cipher correctly.

---

## Phase 3: The Controller (MainActivity)

Now we move to `MainActivity.kt`. This is where the app starts.

**Task:**
Connect your XML buttons to the CryptoManager.

**Step 3.1: Activity Results**
Modern Android doesn't use `onActivityResult` anymore. We use `registerForActivityResult`.
* Create a launcher that listens for `ActivityResultContracts.StartActivityForResult()`.
* When a user picks a file, this launcher will fire and give you a `Uri` (Uniform Resource Identifier).

**Step 3.2: Handling Files**
Write a function `processFile(uri: Uri)`.
* Use `contentResolver.openInputStream(uri)` to get a stream of data from the file the user picked.
* Create a `File` object in your app's internal storage (e.g., `filesDir`) to save the result.
* Pass these streams to your `CryptoManager.encrypt` or `decrypt` functions.

**Step 3.3: Click Listeners**
In `onCreate`, find your buttons using `findViewById`.
* Set an `OnClickListener`.
* Inside the listener, create an `Intent` with action `ACTION_OPEN_DOCUMENT`. This opens the phone's file picker.

---

## Phase 4: Sharing the Result (FileProvider)

Android prevents apps from sharing "raw" file paths (`file://`) for security. You must use a "Content URI" (`content://`). This requires a `FileProvider`.

**Step 4.1: The Provider XML**
Create a new XML file: `res/xml/provider_paths.xml`.
* Define a `<files-path>` tag pointing to your internal root (`/`).

**Step 4.2: The Manifest**
Open `AndroidManifest.xml`.
* Inside the `<application>` tag, add a `<provider>` entry.
* Set `android:name` to `androidx.core.content.FileProvider`.
* Set `android:authorities` to `${applicationId}.provider`.
* Link the `provider_paths.xml` file you created in 4.1.

**Step 4.3: The Intent**
Back in `MainActivity.kt`, write a `shareFile(file: File)` function.
1.  Get the URI using `FileProvider.getUriForFile(...)`.
2.  Create an Intent with action `ACTION_SEND`.
3.  Put the URI as an extra (`EXTRA_STREAM`).
4.  **Crucial:** Add the flag `Intent.FLAG_GRANT_READ_URI_PERMISSION` so the receiving app (like Gmail) has permission to read the file.
5.  Call `startActivity(Intent.createChooser(...))`.

---

## Phase 5: Run and Verify

1.  Connect your phone via USB.
2.  Run the app.
3.  Select an image -> Encrypt it.
4.  Try to open the encrypted file in a file manager (it should be garbage data).
5.  Use the "Decrypt" button on that garbage file -> It should return to the original image.
