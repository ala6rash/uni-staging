# Uni-Connect: Zero to Hero Learning Guide

Everything you need to understand this project — taught using your own code, in plain English.
No abstract examples. Every single concept is shown where it lives inside Uni-Connect.

---

## Table of Contents

1. [C# Basics](#1-c-basics)
   - Variables
   - Data Types
   - If / Else
   - Loops
   - Functions / Methods
   - Classes and Objects
2. [HTML & CSS](#2-html--css)
   - Page Structure
   - Tags
   - CSS Styling
   - Razor: HTML + C# Combined
3. [ASP.NET Core MVC](#3-aspnet-core-mvc)
   - What MVC Means
   - How a Button Click Becomes C# Code
   - The Full Journey: Browser → Server → Database → Browser
   - Controllers
   - Views
   - Models
   - ViewBag
4. [JavaScript](#4-javascript)
   - What JS Does
   - Events
   - fetch() — Sending Data Without Reloading
   - DOM Manipulation
5. [Database: SQL + Entity Framework](#5-database-sql--entity-framework)
   - What a Database Is
   - Tables and Rows
   - How C# Talks to the Database
   - Querying, Filtering, Saving
6. [SignalR — Real-Time Chat](#6-signalr--real-time-chat)
   - Why Normal HTTP Cannot Do Live Chat
   - How SignalR Works
   - The Hub
   - The Browser Side

---

# 1. C# Basics

## What is C#?

C# (pronounced "C sharp") is the programming language that runs on the **server** — the computer that nobody sees. When you click a button on the Uni-Connect website, C# code runs on the server, reads from the database, and sends back a page.

Think of C# as the chef in a restaurant kitchen. The customer (browser) sends an order, the chef (C#) prepares it, and the waiter (HTTP) delivers it.

---

## 1.1 Variables

A **variable** is a named box that holds a value.

### Syntax
```csharp
var name = value;
```

`var` means: "figure out the type automatically."

### Real example — from `LoginController.cs` line 39
```csharp
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
```

Breaking this down word by word:

| Part | Meaning |
|------|---------|
| `var` | Create a variable, figure out the type automatically |
| `user` | The name of the box — you chose this name |
| `=` | Put the result into the box |
| `await` | Wait for this to finish before moving on |
| `_context.Users` | Look in the Users table of the database |
| `.FirstOrDefaultAsync(...)` | Find the first user that matches, or return null if nobody matches |
| `u => u.Email.ToLower() == model.Email.ToLower()` | The rule for matching: "email must match, ignoring capital letters" |

After that line runs, `user` is either a User object (if someone was found) or `null` (nobody found).

### Another example — from `LoginController.cs` line 132
```csharp
string email = model.Email.ToLower().Trim();
```

| Part | Meaning |
|------|---------|
| `string` | This variable will hold text |
| `email` | Name of the box |
| `model.Email` | The email the user typed in the form |
| `.ToLower()` | Convert to all lowercase |
| `.Trim()` | Remove spaces from the start and end |

---

## 1.2 Data Types

Every variable has a **type** — it says what kind of value the box can hold.

| Type | What it holds | Example |
|------|--------------|---------|
| `string` | Text | `"Hello"`, `"ahmad@mail.com"` |
| `int` | Whole number | `42`, `0`, `-5` |
| `bool` | True or false only | `true`, `false` |
| `DateTime` | A date and time | `DateTime.Now` |
| `var` | C# figures it out | whatever the right side is |

### Real examples from `User.cs`
```csharp
public int UserID { get; set; }         // whole number — the database ID
public string Name { get; set; }        // text — the person's name
public string Email { get; set; }       // text — email address
public int Points { get; set; }         // whole number — gamification points
public bool IsDeleted { get; set; }     // true/false — is this account deleted?
public DateTime CreatedAt { get; set; } // date and time — when they registered
```

Each of those is a property of the `User` class (more on classes below).

---

## 1.3 If / Else

C# makes decisions using `if` and `else`. "If this is true, do this. Otherwise, do that."

### From `LoginController.cs` line 99
```csharp
if (user.Role == "Admin")
    return RedirectToAction("AdminDashboard", "Admin");

return RedirectToAction("Dashboard", "Dashboard");
```

Plain English:
- If the logged-in user is an Admin → send them to the Admin dashboard.
- Otherwise (regular student) → send them to the student dashboard.

### From `LoginController.cs` line 42
```csharp
if (user != null && user.AccountLockedUntil.HasValue && user.AccountLockedUntil > DateTime.Now)
{
    TimeSpan timeRemaining = user.AccountLockedUntil.Value - DateTime.Now;
    ModelState.AddModelError("",
        $"Account temporarily locked. Try again in {(int)timeRemaining.TotalMinutes + 1} minutes.");
    return View(model);
}
```

Plain English:
- If the user exists AND their account is locked AND the lock hasn't expired yet → show them an error message telling them to wait.

`&&` means AND — ALL conditions must be true.

### From `DashboardController.cs` line 60
```csharp
if (filter == "Faculty")
{
    query = query.Where(p => p.User.Faculty == user.Faculty);
}
else if (filter == "Trending")
{
    query = query.Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                 .OrderByDescending(p => p.ViewsCount + p.Upvotes);
}
```

Plain English:
- If the filter button clicked was "Faculty" → only show posts from the same faculty.
- Else if the filter was "Trending" → only show posts from the last 7 days, sorted by popularity.

---

## 1.4 Functions / Methods

A **method** is a named set of instructions you can run by calling its name.

### Syntax
```csharp
public ReturnType MethodName(Parameters)
{
    // instructions
    return something;
}
```

### Real example — `GetCurrentUser()` from `DashboardController.cs` line 679
```csharp
private async Task<User> GetCurrentUser()
{
    var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdStr)) return null;

    int userId = int.Parse(userIdStr);
    return await _context.Users
        .Include(u => u.Notifications)
        .FirstOrDefaultAsync(u => u.UserID == userId);
}
```

Breaking it down:

| Part | Meaning |
|------|---------|
| `private` | Only this class can use this method |
| `async Task<User>` | This method runs asynchronously and returns a `User` object when done |
| `GetCurrentUser` | The name we gave it |
| `()` | No parameters needed |
| Inside the `{}` | The instructions |
| `return null` | If we can't find the user, return nothing |
| `return await ...` | Find the user in the database and return them |

This method is used many times throughout the file:
```csharp
var user = await GetCurrentUser();
if (user == null) return RedirectToAction("Login_Page", "Login");
```

That's the power of methods: write once, use everywhere.

### Another example — `SaveImage()` from `DashboardController.cs` line 718
```csharp
private async Task<string?> SaveImage(IFormFile? file, string folder)
```

| Part | Meaning |
|------|---------|
| `string?` | Returns text (or null if something went wrong) |
| `IFormFile? file` | An uploaded image file (optional — can be null) |
| `string folder` | Which folder to save it in (text parameter) |

---

## 1.5 Classes and Objects

A **class** is a blueprint. An **object** is one thing built from that blueprint.

Think of a class like a cookie cutter, and objects are the actual cookies.

### Real example — `User.cs`
```csharp
public class User
{
    public int UserID { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }
    public int Points { get; set; }
    public bool IsDeleted { get; set; } = false;
    // ...
}
```

`User` is the blueprint. Every student in the database is one `User` object.

### Creating an object — from `LoginController.cs` line 162
```csharp
var newUser = new User
{
    UniversityID = universityId,
    Name = model.Name,
    Username = universityId,
    Email = email,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
    Role = "Student",
    Faculty = model.Faculty,
    YearOfStudy = model.YearOfStudy,
    Points = 0,
    CreatedAt = DateTime.UtcNow
};
```

`new User { ... }` creates one fresh user object and fills in all the fields. Then:
```csharp
_context.Users.Add(newUser);
await _context.SaveChangesAsync();
```
This saves it to the database.

### The `{ get; set; }` thing

```csharp
public string Name { get; set; }
```

`get` = you can read the value: `Console.WriteLine(user.Name)`
`set` = you can change the value: `user.Name = "Ahmad"`

---

# 2. HTML & CSS

## What is HTML?

HTML builds the **skeleton** of the page. It tells the browser what elements exist — headings, paragraphs, buttons, forms.

## What is CSS?

CSS decides how things **look** — colors, sizes, spacing, fonts.

---

## 2.1 HTML Page Structure

Every HTML page follows this shape:

```html
<!DOCTYPE html>
<html>
<head>
    <title>Page Title</title>
    <link href="style.css" rel="stylesheet" />
</head>
<body>
    <!-- everything the user sees goes here -->
</body>
</html>
```

### Real example — from `Login_Page.cshtml` lines 7-15
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1.0" />
    <title>Sign In — UniConnect</title>
    <link href="~/css/login.css" rel="stylesheet" />
</head>
<body class="auth-bg">
```

| Part | Meaning |
|------|---------|
| `<!DOCTYPE html>` | Tell the browser this is a modern HTML file |
| `<html lang="en">` | The root element — everything goes inside |
| `<head>` | Invisible setup: title, CSS links, scripts |
| `<meta charset="UTF-8">` | Support all characters including Arabic |
| `<link href="~/css/login.css">` | Load the CSS file for this page |
| `<body class="auth-bg">` | The visible part of the page; `auth-bg` is a CSS class that sets the background |

---

## 2.2 Common HTML Tags

| Tag | What it does | Example |
|-----|-------------|---------|
| `<h1>` to `<h6>` | Headings (h1 biggest, h6 smallest) | `<h1>Welcome</h1>` |
| `<p>` | Paragraph of text | `<p>Hello world</p>` |
| `<a>` | A link | `<a href="/home">Go home</a>` |
| `<button>` | A clickable button | `<button>Click me</button>` |
| `<form>` | A form to send data | `<form method="post">` |
| `<input>` | A text/password field | `<input type="email">` |
| `<div>` | An invisible box to group things | `<div class="card">` |
| `<span>` | Inline container | `<span class="badge">Admin</span>` |
| `<table>` | A table of data | see ManageUsers below |
| `<img>` | An image | `<img src="/photo.png">` |

### Real example — login form from `Login_Page.cshtml` lines 38-65
```html
<form asp-action="Login_Page" method="post">
    @Html.AntiForgeryToken()

    <div class="form-group">
        <label class="form-label" asp-for="Email">University Email</label>
        <input class="form-input" asp-for="Email" type="email" placeholder="StudentID@philadelphia.edu.jo" />
        <span asp-validation-for="Email" class="field-validation-error"></span>
    </div>

    <div class="form-group">
        <label class="form-label" asp-for="Password">Password</label>
        <input class="form-input" asp-for="Password" type="password" placeholder="Enter your password" />
    </div>

    <button type="submit" class="btn btn-primary btn-full btn-lg">Sign In →</button>
</form>
```

| Part | Meaning |
|------|---------|
| `<form method="post">` | When submitted, send data to the server using POST |
| `asp-action="Login_Page"` | Send the data to the `Login_Page` action in the controller |
| `<input type="email">` | Email text field |
| `<input type="password">` | Password field (hides characters) |
| `<button type="submit">` | Clicking this submits the whole form |
| `placeholder="..."` | Grey hint text shown before the user types |
| `class="btn btn-primary"` | Apply CSS styling from the stylesheet |

---

## 2.3 CSS — Making Things Look Good

CSS rules follow this pattern:
```css
selector {
    property: value;
}
```

A **selector** targets an HTML element. A **property** changes something about it.

### Real examples from `ManageUsers.cshtml` lines 80-105
```css
.data-table {
    width: 100%;
    border-collapse: collapse;
}
.data-table th {
    text-align: left;
    padding: 16px;
    background: #F8FAFC;
    font-weight: 600;
    color: var(--text-1);
    font-size: 14px;
    border-bottom: 1px solid #E2E8F0;
}
.btn-danger {
    background: #E11D48;
    color: white;
    border: none;
}
```

| Part | Meaning |
|------|---------|
| `.data-table` | Target any element with `class="data-table"` |
| `width: 100%` | Make it stretch the full width |
| `border-collapse: collapse` | No double borders between cells |
| `.data-table th` | Target the `<th>` header cells inside `.data-table` |
| `padding: 16px` | Add 16 pixels of space inside the cell |
| `var(--text-1)` | Use a color variable defined elsewhere (CSS variable) |
| `.btn-danger` | Target anything with `class="btn-danger"` |
| `background: #E11D48` | Set the background to a dark red color |

### CSS Variables — how the design system works

In `design-system.css` there are lines like:
```css
:root {
    --indigo: #3D52A0;
    --text-1: #0F172A;
    --border: #E2E8F0;
}
```

These define global colors once. Now any file can use `var(--indigo)` instead of repeating the hex code everywhere. Change it in one place, and the whole app updates.

---

## 2.4 Razor — HTML + C# Combined

The `.cshtml` files in `Views/` are **Razor** files. They are HTML files with C# mixed in. Anything starting with `@` is C#.

### Real example — from `ManageUsers.cshtml` lines 30-53
```html
@foreach (var user in Model)
{
    <tr>
        <td class="text-mono">@user.UniversityID</td>
        <td>
            <div style="display:flex; align-items:center; gap:12px;">
                <div>
                    <div style="font-weight:600;">@user.Name</div>
                    <div class="text-3">@@@user.Username</div>
                </div>
            </div>
        </td>
        <td>@user.Email</td>
        <td><span class="badge badge-primary">@user.Role</span></td>
        <td>
            @if (user.IsDeleted)
            {
                <span class="badge" style="background:#FFF1F2; color:#E11D48;">Deleted</span>
            }
            else
            {
                <span class="badge" style="background:#ECFDF5; color:#059669;">Active</span>
            }
        </td>
    </tr>
}
```

| Part | Meaning |
|------|---------|
| `@foreach (var user in Model)` | Loop through every user passed from the controller |
| `@user.UniversityID` | Output the user's university ID as text on the page |
| `@user.Name` | Output their name |
| `@@@user.Username` | The `@@@` is: first `@@` = display a literal `@` sign, then `@user.Username` = display the username |
| `@if (user.IsDeleted)` | C# if-statement inside the HTML |

The browser never sees `@foreach` or `@if`. The server runs that C# code, generates plain HTML, and sends the finished HTML to the browser.

---

# 3. ASP.NET Core MVC

## What does MVC mean?

MVC stands for **Model, View, Controller**. It is a way of organising code so each part has one job.

| Letter | Name | Job | Folder in project |
|--------|------|-----|-------------------|
| M | Model | Represents data (a User, a Post, a Session) | `Models/` |
| V | View | The HTML page the user sees | `Views/` |
| C | Controller | Receives requests, talks to the database, sends data to the view | `Controllers/` |

---

## 3.1 The Full Journey: Browser → Server → Browser

Let's trace exactly what happens when you click **Sign In**.

### Step 1 — Browser sends a request
The form in `Login_Page.cshtml` has `method="post"` and `asp-action="Login_Page"`.
When you click "Sign In →", the browser sends a POST request to `/Login/Login_Page` with the email and password attached.

### Step 2 — ASP.NET routes it to the right Controller + Action
The framework sees `/Login/Login_Page` and sends it to `LoginController`, method `Login_Page(LoginViewModel model)`.

### Step 3 — Controller runs C# code
From `LoginController.cs` line 28:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login_Page(LoginViewModel model)
{
    // 1. Check if form is valid
    if (!ModelState.IsValid)
        return View(model);

    // 2. Find user in database
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

    // 3. Check password
    bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);

    // 4. If wrong password, show error
    if (!isPasswordCorrect)
    {
        ModelState.AddModelError("", "Invalid email or password.");
        return View(model);
    }

    // 5. If correct, sign them in and redirect
    return RedirectToAction("Dashboard", "Dashboard");
}
```

### Step 4 — Controller sends a response
Either `return View(model)` (show the login page again with an error) or `return RedirectToAction("Dashboard", "Dashboard")` (send them to the dashboard).

### Step 5 — Browser receives and displays the result

---

## 3.2 Controllers

A controller is a C# class that handles requests. Every **public method** in a controller is called an **action** and maps to a URL.

| Action method | Maps to URL |
|--------------|-------------|
| `Login_Page()` in `LoginController` | `/Login/Login_Page` |
| `Dashboard()` in `DashboardController` | `/Dashboard/Dashboard` |
| `Sessions()` in `DashboardController` | `/Dashboard/Sessions` |
| `SinglePost(int id)` in `DashboardController` | `/Dashboard/SinglePost?id=5` |

### `[HttpGet]` and `[HttpPost]`

```csharp
[HttpGet]
public IActionResult Login_Page()        // Show the empty login form
{
    return View(new LoginViewModel());
}

[HttpPost]
public async Task<IActionResult> Login_Page(LoginViewModel model)  // Process submitted form
{
    // ...
}
```

Both have the same name but different attributes. `[HttpGet]` handles when you visit the URL. `[HttpPost]` handles when you submit a form.

### `[Authorize]`

```csharp
[Authorize]
public class DashboardController : Controller
```

This one attribute on the class makes every action in the controller require login. If you're not logged in and you try to visit `/Dashboard/Dashboard`, ASP.NET automatically redirects you to the login page. One line protects the entire controller.

---

## 3.3 Models

A model is a C# class that represents data. There are two kinds in this project:

### Database Models (in `Models/`)

These map directly to database tables.

```csharp
// Models/User.cs — maps to the Users table
public class User
{
    public int UserID { get; set; }    // column: UserID
    public string Name { get; set; }   // column: Name
    public string Email { get; set; }  // column: Email
    public int Points { get; set; }    // column: Points
}
```

```csharp
// Models/Post.cs — maps to the Posts table
public class Post
{
    public int PostID { get; set; }
    public int UserID { get; set; }    // foreign key — which user wrote this
    public string Title { get; set; }
    public string Content { get; set; }
    public int Upvotes { get; set; }
    public bool IsDeleted { get; set; }

    public User User { get; set; }     // navigation — lets you do post.User.Name
}
```

### ViewModels (in `ViewModels/`)

These carry data specifically for one view. They don't map to tables.

From the login form, the view needs an Email field and a Password field. So there is a `LoginViewModel`:
```csharp
public class LoginViewModel
{
    public string Email { get; set; }
    public string Password { get; set; }
}
```

When the form is submitted, ASP.NET automatically fills in the `LoginViewModel` from the form fields and passes it to the controller method.

---

## 3.4 ViewBag — Passing Extra Data to Views

The controller can pass one main object to the view (the Model), but sometimes you need to pass extra things. That is what `ViewBag` is for.

### From `DashboardController.cs`
```csharp
var posts = await query.ToListAsync();

ViewBag.Posts = posts;          // pass the posts list
ViewBag.SearchQuery = search;   // pass what the user searched for
ViewBag.ActiveFilter = filter ?? "All";  // pass which filter is active

return View(user);  // the main model is the user
```

Then in the view:
```html
@foreach (var post in ViewBag.Posts)
{
    <div>@post.Title</div>
}
```

`ViewBag` is a bag you can stuff anything into. The view reaches in and takes out what it needs.

---

# 4. JavaScript

## What does JavaScript do?

C# runs on the server (nobody sees it). HTML/CSS build the page. JavaScript runs in **the browser** — it makes the page interactive without reloading.

Without JS: every button click sends you to a new page.
With JS: things happen instantly on the page without refreshing.

---

## 4.1 Events — Reacting to User Actions

```javascript
document.getElementById("myButton").addEventListener("click", function() {
    alert("You clicked!");
});
```

`addEventListener("click", ...)` means: when this element is clicked, run this function.

---

## 4.2 fetch() — Talking to the Server Without Reloading

`fetch()` sends a request to the server in the background and gets a response, all without the page reloading. It is how the chat loads messages, and how upvoting works.

### Real example — from `SinglePost.cshtml` (the session request button)
```javascript
async function submitSessionRequest() {
    const description = document.getElementById('sessionDescription').value;
    const postId = /* post id */;

    const response = await fetch('/Session/SendRequest', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('[name=__RequestVerificationToken]').value
        },
        body: JSON.stringify({ postId: postId, description: description })
    });

    if (response.ok) {
        alert('Request sent!');
    }
}
```

Breaking it down:

| Part | Meaning |
|------|---------|
| `async function` | This function can use `await` inside |
| `fetch('/Session/SendRequest', {...})` | Send a request to that URL |
| `method: 'POST'` | This is a POST request (sending data) |
| `headers` | Extra info attached to the request |
| `body: JSON.stringify(...)` | The actual data being sent (as JSON text) |
| `await` | Wait for the server to respond |
| `response.ok` | Did the server say success (HTTP 200)? |

### Real example — upvoting a post without refreshing
```javascript
document.querySelectorAll('.upvote-btn').forEach(btn => {
    btn.addEventListener('click', async function() {
        const postId = this.dataset.postId;
        const response = await fetch('/Dashboard/UpvotePost', {
            method: 'POST',
            body: new FormData(this.closest('form'))
        });
        if (response.ok) {
            const data = await response.json();
            this.querySelector('.upvote-count').textContent = data.upvotes;
        }
    });
});
```

The upvote count number on the page changes instantly. No page reload.

---

## 4.3 DOM Manipulation — Changing the Page

"DOM" = Document Object Model = the live tree of HTML elements in the browser.

JS can read, change, add, or remove any element on the page.

```javascript
// Read content
let text = document.getElementById("myDiv").textContent;

// Change content
document.getElementById("myDiv").textContent = "New text";

// Change style
document.getElementById("myDiv").style.display = "none";  // hide it

// Add a new element
let div = document.createElement("div");
div.textContent = "I was added by JS";
document.body.appendChild(div);
```

### Real example — appending a chat message to the screen
```javascript
function appendMessage(senderId, currentUserId, text, time) {
    const isMine = senderId == currentUserId;
    const msgDiv = document.createElement('div');
    msgDiv.className = 'msg ' + (isMine ? 'msg-mine' : 'msg-theirs');
    msgDiv.innerHTML = `
        <div class="msg-bubble">${text}</div>
        <div class="msg-time">${time}</div>
    `;
    document.getElementById('chatMessages').appendChild(msgDiv);
    document.getElementById('chatMessages').scrollTop = 99999;
}
```

When a new chat message arrives (from SignalR), `appendMessage()` creates a new HTML element and adds it to the bottom of the chat. The user sees it instantly.

---

# 5. Database: SQL + Entity Framework

## What is a database?

A database is a structured place to store data permanently. When you turn off the server, the data is still there.

Think of it as a collection of **spreadsheets** (called tables).

---

## 5.1 Tables and Rows

Each database table is like a spreadsheet:
- **Columns** = properties (Name, Email, Points…)
- **Rows** = individual records (one row = one user)

| UserID | Name | Email | Points | Role |
|--------|------|-------|--------|------|
| 1 | Ahmad | ahmad@philadelphia.edu.jo | 150 | Student |
| 2 | Sara | sara@philadelphia.edu.jo | 320 | Student |
| 3 | Admin | admin@uni.jo | 0 | Admin |

Every table in Uni-Connect is declared in `ApplicationDbContext.cs`:
```csharp
public DbSet<User> Users { get; set; }        // the Users table
public DbSet<Post> Posts { get; set; }        // the Posts table
public DbSet<Message> Messages { get; set; }  // the Messages table
// ...and so on
```

---

## 5.2 Entity Framework — C# Talks to SQL

Normally, to get data from a database you write SQL:
```sql
SELECT * FROM Users WHERE Email = 'ahmad@mail.com'
```

Entity Framework (EF) lets you write that in C# instead:
```csharp
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == "ahmad@mail.com");
```

EF translates your C# into SQL automatically. You never write raw SQL.

---

## 5.3 The DbContext

`ApplicationDbContext` is the class that represents your database connection. It is always injected into controllers:

```csharp
public class LoginController : Controller
{
    private readonly ApplicationDbContext _context;

    public LoginController(ApplicationDbContext context)
    {
        _context = context;  // framework gives you this automatically
    }
}
```

`_context` is your gateway to every table. `_context.Users` = the Users table. `_context.Posts` = Posts table. Etc.

---

## 5.4 Common Database Operations

### Finding one record
```csharp
// Find the first user whose email matches (or null if not found)
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email);
```

### Finding many records
```csharp
// Get all non-deleted posts, newest first
var posts = await _context.Posts
    .Where(p => !p.IsDeleted)
    .OrderByDescending(p => p.CreatedAt)
    .ToListAsync();
```

### Filtering
```csharp
// Only show posts from the same faculty as the logged-in user
query = query.Where(p => p.User.Faculty == user.Faculty);
```

### Including related data (JOIN)
```csharp
// Get posts AND their author AND their answers
var posts = await _context.Posts
    .Include(p => p.User)           // attach User object to each post
    .Include(p => p.Answers)        // attach list of Answers to each post
    .ToListAsync();
```

Without `.Include()`, `post.User` would be null even though the data exists in the database. `.Include()` is how you tell EF to fetch related rows.

### Creating a new record
```csharp
var newUser = new User { Name = "Ahmad", Email = "ahmad@mail.com", ... };
_context.Users.Add(newUser);
await _context.SaveChangesAsync();  // actually writes to database
```

### Updating a record
```csharp
user.Points = user.Points + 50;    // change in memory
_context.Users.Update(user);       // mark as changed
await _context.SaveChangesAsync(); // save to database
```

### Soft delete (marking as deleted, not actually deleting)
```csharp
post.IsDeleted = true;             // set the flag
await _context.SaveChangesAsync(); // save
```

The post still exists in the database, but every query filters it out with `.Where(p => !p.IsDeleted)`. This lets you recover deleted content if needed.

---

## 5.5 Relationships Between Tables

Real data is connected. A Post belongs to a User. A Message belongs to a Session. These connections are called **foreign keys**.

### Example from `Post.cs`
```csharp
public class Post
{
    public int UserID { get; set; }   // foreign key — which user wrote this post
    public User User { get; set; }    // navigation property — gives you the full User object
}
```

And from `ApplicationDbContext.cs`:
```csharp
modelBuilder.Entity<Post>()
    .HasOne(p => p.User)           // each Post has one User
    .WithMany(u => u.Posts)        // each User has many Posts
    .HasForeignKey(p => p.UserID)  // the foreign key column is UserID
    .OnDelete(DeleteBehavior.NoAction);
```

Plain English: "A Post belongs to one User. A User can have many Posts. They are linked by the UserID column."

---

## 5.6 Migrations — Keeping the Database in Sync

When you change a Model (add a column, rename something), you run:
```
dotnet ef migrations add SomeDescription
dotnet ef database update
```

EF generates a file in `Migrations/` that describes the change in SQL. `database update` applies it to the actual database. This is how the database structure stays in sync with the C# models.

---

# 6. SignalR — Real-Time Chat

## 6.1 Why Normal HTTP Cannot Do Live Chat

Normal HTTP works like this:
1. Browser asks: "Any new messages?"
2. Server answers: "No."
3. Browser asks again one second later: "Any new messages?"
4. Server answers: "No."
5. (repeat forever)

This is called **polling** — very wasteful. And there is always a delay.

With normal HTTP, you cannot push a message to the browser the moment it arrives. The browser has to ask first.

---

## 6.2 How SignalR Works

SignalR keeps a **permanent open connection** between the browser and the server (called a WebSocket).

1. Ahmad sends a message.
2. Server receives it instantly.
3. Server immediately pushes it to Sara's browser through the open connection.
4. Sara sees it appear in under a second.

No polling. No delay.

---

## 6.3 The Hub — Server Side

`ChatHub.cs` is the SignalR Hub — the server-side class that handles real-time connections.

```csharp
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
    }

    public async Task SendMessage(string roomId, string message)
    {
        var senderId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var sessionId = int.Parse(roomId);

        // Security check: is this person in the session?
        var session = await _context.PrivateSessions.FindAsync(sessionId);
        if (session == null || !session.IsActive ||
            (session.StudentID != senderId && session.HelperID != senderId))
        {
            throw new HubException("Not authorized to send in this session.");
        }

        // Save message to database
        var newMessage = new Message
        {
            SessionID = sessionId,
            SenderID = senderId,
            MessageText = message,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Messages.Add(newMessage);
        await _context.SaveChangesAsync();

        // Broadcast to everyone in the room
        await Clients.Group(roomId).SendAsync(
            "ReceiveMessage",
            Context.ConnectionId,
            senderId,
            message,
            newMessage.SentAt.ToString("HH:mm")
        );
    }
}
```

Key concepts:

| Concept | Meaning |
|---------|---------|
| `Hub` | Base class — gives you Groups, Clients, Context |
| `Groups.AddToGroupAsync(connectionId, roomId)` | Add this user to a chat room |
| `Context.ConnectionId` | This user's unique connection identifier |
| `Context.User` | The logged-in user (same as in Controllers) |
| `Clients.Group(roomId).SendAsync("ReceiveMessage", ...)` | Push a message to everyone in this room |
| `throw new HubException(...)` | Reject the request — client receives an error |

**Groups** = chat rooms. When Ahmad opens session 7, he calls `JoinRoom("7")` — he is added to group "7". When Sara sends a message to room "7", everyone in that group (including Ahmad) receives it instantly.

---

## 6.4 The Browser Side — JavaScript

In `ChatPage.cshtml`, JavaScript connects to the hub and handles incoming messages.

```javascript
// 1. Create a connection to the hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

// 2. What to do when a message arrives
connection.on("ReceiveMessage", function(connectionId, senderId, text, time) {
    appendMessage(senderId, currentUserId, text, time);
});

// 3. Start the connection
connection.start().then(function() {
    // 4. Join the chat room for this session
    connection.invoke("JoinRoom", sessionId.toString());
    // 5. Load chat history
    loadHistory(sessionId);
});

// 6. Send a message
async function sendMsg() {
    const text = document.getElementById('msgInput').value.trim();
    if (!text) return;

    await connection.invoke("SendMessage", sessionId.toString(), text);
    document.getElementById('msgInput').value = '';
}
```

Line by line:

| Part | Meaning |
|------|---------|
| `new signalR.HubConnectionBuilder().withUrl("/chatHub").build()` | Create a connection object pointing at your hub URL |
| `connection.on("ReceiveMessage", function(...))` | Register a listener: when the server sends "ReceiveMessage", run this function |
| `connection.start()` | Open the WebSocket connection to the server |
| `connection.invoke("JoinRoom", sessionId)` | Call the `JoinRoom` method on the server-side Hub |
| `connection.invoke("SendMessage", roomId, text)` | Call `SendMessage` on the Hub — server saves it and broadcasts to everyone |

The flow when Ahmad types and presses Send:
1. Browser calls `connection.invoke("SendMessage", "7", "Hello Sara")`.
2. Hub's `SendMessage` runs — saves to DB, calls `Clients.Group("7").SendAsync("ReceiveMessage", ...)`.
3. Sara's browser (connected to the same hub, in the same group) has `connection.on("ReceiveMessage", ...)` registered.
4. Sara's handler fires — `appendMessage()` adds the bubble to her screen.
5. Total time: under 100 milliseconds.

---

# How Everything Connects Together

Here is the big picture of what happens when Ahmad sends a message in a chat session:

```
Ahmad's Browser
  │
  │  JS: connection.invoke("SendMessage", "7", "Hello")
  │
  ▼
ASP.NET Core Server
  │
  │  ChatHub.SendMessage("7", "Hello") runs
  │  ├── Validates Ahmad is in session 7
  │  ├── Saves Message to database via _context
  │  └── Calls Clients.Group("7").SendAsync("ReceiveMessage", ...)
  │
  ▼
SQL Server Database
  │  INSERT INTO Messages (SessionID, SenderID, MessageText, SentAt)
  │  VALUES (7, 1, 'Hello', '2026-05-01 12:00:00')
  │
  ▼
ASP.NET Core Server (push back)
  │
  │  SignalR pushes "ReceiveMessage" event to all in group "7"
  │
  ▼
Sara's Browser
  │
  │  JS: connection.on("ReceiveMessage", ...) fires
  │  └── appendMessage() adds the bubble to her screen
  │
  ▼
Sara sees "Hello" appear instantly
```

Every technology you learned in this guide plays its role:
- **C#** — the logic (checking, saving, broadcasting)
- **HTML/CSS** — what the chat page looks like
- **Razor** — combining C# data with HTML
- **ASP.NET MVC** — routing requests to the right controller
- **JavaScript** — making the page live (no reload)
- **Entity Framework** — saving messages to the database
- **SignalR** — the real-time connection

---

# Quick Reference

## C# Cheat Sheet

```csharp
// Variable
var name = "Ahmad";
string email = "ahmad@mail.com";
int points = 150;
bool isDeleted = false;

// If / else
if (points > 100) { /* do something */ }
else { /* do something else */ }

// Method
public async Task<User> GetUser(int id)
{
    return await _context.Users.FindAsync(id);
}

// Class
public class User
{
    public int UserID { get; set; }
    public string Name { get; set; }
}

// Create object
var user = new User { UserID = 1, Name = "Ahmad" };

// Database: query
var posts = await _context.Posts.Where(p => !p.IsDeleted).ToListAsync();

// Database: save
_context.Posts.Add(post);
await _context.SaveChangesAsync();
```

## HTML Cheat Sheet

```html
<h1>Big heading</h1>
<p>Paragraph</p>
<a href="/page">Link</a>
<button type="submit">Click me</button>
<input type="text" placeholder="Type here" />
<form method="post" action="/url"> ... </form>
<div class="card"> ... </div>
<span class="badge">Label</span>
```

## CSS Cheat Sheet

```css
.my-class {
    color: red;
    background: blue;
    font-size: 16px;
    padding: 12px;
    margin: 8px;
    border: 1px solid #ccc;
    border-radius: 8px;
    display: flex;
    width: 100%;
}
```

## JavaScript Cheat Sheet

```javascript
// Get element
const btn = document.getElementById("myBtn");

// React to click
btn.addEventListener("click", function() { /* ... */ });

// Change text
document.getElementById("count").textContent = "5";

// Send to server (POST)
const response = await fetch("/Dashboard/UpvotePost", {
    method: "POST",
    body: formData
});
const data = await response.json();

// SignalR: send
await connection.invoke("SendMessage", roomId, text);

// SignalR: receive
connection.on("ReceiveMessage", function(senderId, text, time) { /* ... */ });
```

---

*This guide was written specifically for the Uni-Connect project. Every example comes from code you can find in this repository.*
