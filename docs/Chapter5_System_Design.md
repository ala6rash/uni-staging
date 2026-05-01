# CHAPTER 5 – SYSTEM DESIGN
### UniConnect — Peer Learning & Tutoring Platform
**Philadelphia University | Department of Computer Science**
**Group 2 — Training For Students**

---

## 5.1 Introduction

This chapter translates the requirements specification defined in earlier chapters into a concrete architectural and technical design for the UniConnect platform. UniConnect is a web-based peer-learning community built exclusively for Philadelphia University students, enabling them to ask academic questions, receive answers from peers, engage in private tutoring sessions, and earn points redeemable at campus venues.

The design decisions documented here are driven by four principles:

| Principle | Decision |
|---|---|
| **Security** | BCrypt password hashing, anti-CSRF tokens, role-based authorization, soft-delete pattern |
| **Scalability** | Layered MVC architecture, service abstraction, EF Core migrations |
| **Maintainability** | Separation of concerns via IPostService / IPointService, single source of truth in ApplicationDbContext |
| **Usability** | Progressive wizard UI, real-time point feedback via SignalR, responsive design |

---

## 5.2 System Architecture Design

### 5.2.1 Architecture Overview

UniConnect follows the **Model-View-Controller (MVC)** architectural pattern implemented on **ASP.NET Core 8.0**. The system is organized into five distinct layers:

```
┌─────────────────────────────────────────────────────┐
│                 PRESENTATION LAYER                  │
│  Razor Views (.cshtml) + JavaScript (Lucide Icons)  │
│  _DashboardLayout | _Layout | Individual Views      │
└──────────────────────┬──────────────────────────────┘
                       │ HTTP Requests / Responses
┌──────────────────────▼──────────────────────────────┐
│               APPLICATION LAYER                     │
│  HomeController | LoginController                   │
│  DashboardController | AdminController              │
│  ─────────────────────────────────                  │
│  IPostService / PostService                         │
│  IPointService / PointService                       │
└──────────────────────┬──────────────────────────────┘
                       │ LINQ / EF Core Queries
┌──────────────────────▼──────────────────────────────┐
│                   DATA LAYER                        │
│  ApplicationDbContext (Entity Framework Core 9.0)   │
│  SQLite (Development) | SQL Server (Production)     │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│               INTEGRATION LAYER                     │
│  SignalR — ChatHub (real-time private sessions)      │
│  ui-avatars.com API (auto-generated avatars)        │
│  [Future] SMTP Email | QR Code API                  │
└─────────────────────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│                SECURITY LAYER                       │
│  BCrypt Password Hashing (cost factor 12)           │
│  Cookie-based Authentication (24-hour expiry)       │
│  Anti-CSRF Tokens on all POST endpoints             │
│  [Authorize] + [Authorize(Roles="Admin")] guards    │
│  Account lockout (5 failed attempts)                │
│  Email domain restriction (@philadelphia.edu.jo)    │
└─────────────────────────────────────────────────────┘
```

### 5.2.2 Layer Descriptions

**Presentation Layer**
All user interfaces are built using ASP.NET Razor Views (`.cshtml`). Two shared layout templates provide consistent navigation:
- `_DashboardLayout.cshtml` — authenticated users (sidebar, navbar, notifications)
- `_Layout.cshtml` — public pages (landing page, login, register)

Icons are rendered using the **Lucide** SVG icon library (loaded via CDN). Styling uses a single custom design system (`design-system.css`) built with CSS custom properties for theming.

**Application Layer**
Request routing is handled by four controllers:

| Controller | Responsibility |
|---|---|
| `HomeController` | Landing page with live statistics from DB |
| `LoginController` | Registration, login, logout, password reset |
| `DashboardController` | All student features (feed, posts, profile, points, sessions) |
| `AdminController` | User management, report moderation, post endorsement |

Business logic is encapsulated in two service classes injected via dependency injection:
- `IPostService / PostService` — post creation/deletion, answer management, upvote tracking, tutoring requests, image upload with validation (max 5MB, allowed types: jpg/png/gif)
- `IPointService / PointService` — points award/deduction transactions, level calculation, reward redemption

**Data Layer**
`ApplicationDbContext` manages all database operations using Code-First EF Core with automatic migration support. Global query filters enforce soft-delete across all entity queries (records with `IsDeleted = true` are automatically excluded).

**Integration Layer**
- **SignalR ChatHub** — enables real-time bidirectional messaging for private tutoring sessions. Room membership is verified before joining to prevent session hijacking.
- **ui-avatars.com** — generates avatar images from user names when no profile photo is uploaded.
- *[Planned]* SMTP integration for password reset email delivery and notification emails.
- *[Planned]* QR Code API for campus reward redemption at physical venues.

**Security Layer**
See Section 5.2.3 for full security design.

### 5.2.3 Security Design

| Mechanism | Implementation |
|---|---|
| Password storage | BCrypt hashing (salt + cost factor 12) via BCrypt.Net-Next |
| Session management | Cookie-based authentication, 24-hour sliding expiry |
| CSRF protection | `[ValidateAntiForgeryToken]` on all POST endpoints |
| Authorization | `[Authorize]` on `DashboardController`; `[Authorize(Roles="Admin")]` on `AdminController` |
| Account lockout | 5 failed login attempts triggers 15-minute lockout |
| Input validation | Email domain restricted to `@philadelphia.edu.jo` via ViewModel DataAnnotations |
| File upload safety | Extension whitelist (jpg/jpeg/png/gif), 5MB size limit, GUID-renamed filenames |
| Data integrity | Soft-delete pattern — no records are physically deleted from the database |
| Session privacy | `GetMessages` and `ChatHub.JoinRoom` verify session membership before serving data |

**Future Security Enhancements (planned for production deployment):**
- HTTPS enforcement with HSTS headers
- Rate limiting on login and registration endpoints
- Email verification for new accounts before activation
- Content Security Policy (CSP) headers

---

## 5.3 Class Diagram

### 5.3.1 Core Domain Classes

```
┌─────────────────────────────────────────────┐
│                    User                     │
├─────────────────────────────────────────────┤
│ + UserID: int (PK)                          │
│ + UniversityID: string                      │
│ + Name: string                              │
│ + Username: string                          │
│ + Email: string                             │
│ + PasswordHash: string                      │
│ + Role: string ("Student" | "Admin")        │
│ + Faculty: string                           │
│ + YearOfStudy: string                       │
│ + Points: int                               │
│ + ProfileImageUrl: string?                  │
│ + IsDeleted: bool                           │
│ + CreatedAt: DateTime                       │
├─────────────────────────────────────────────┤
│ + Posts: ICollection<Post>                  │
│ + Answers: ICollection<Answer>              │
│ + Notifications: ICollection<Notification>  │
│ + PointsTransactions: ICollection<Pts..>    │
└─────────────────────────────────────────────┘
           │ 1            1 │
           │◄─────────────►│
           ▼                ▼
┌──────────────────┐   ┌──────────────────────┐
│       Post       │   │        Answer        │
├──────────────────┤   ├──────────────────────┤
│ + PostID: int    │   │ + AnswerID: int       │
│ + Title: string  │   │ + Content: string     │
│ + Content: string│   │ + PostID: int (FK)   │
│ + UserID: int    │   │ + UserID: int (FK)   │
│ + CategoryID: int│   │ + IsAccepted: bool   │
│ + CourseCode: str│   │ + Upvotes: int        │
│ + Upvotes: int   │   │ + ImageUrl: string?  │
│ + ViewsCount: int│   │ + IsDeleted: bool    │
│ + IsEndorsed: bool│  │ + CreatedAt: DateTime│
│ + ImageUrl: str? │   └──────────────────────┘
│ + IsDeleted: bool│
│ + CreatedAt: DT  │
└──────────────────┘
        │ M           N │
        ▼               ▼
┌──────────────┐   ┌──────────────┐
│   Category   │   │     Tag      │
├──────────────┤   ├──────────────┤
│ + CategoryID │   │ + TagID: int │
│ + Name: str  │   │ + Name: str  │
│ + Faculty: ? │   └──────────────┘
└──────────────┘         │
                   ┌─────┴──────┐
                   │  PostTag   │ (junction)
                   ├────────────┤
                   │ + PostID   │
                   │ + TagID    │
                   └────────────┘

┌─────────────────────────────────────────────┐
│               PrivateSession                │
├─────────────────────────────────────────────┤
│ + PrivateSessionID: int (PK)                │
│ + RequestID: int (FK → Request)             │
│ + StudentID: int (FK → User)                │
│ + HelperID: int (FK → User)                 │
│ + IsDeleted: bool                           │
│ + CreatedAt: DateTime                       │
├─────────────────────────────────────────────┤
│ + Messages: ICollection<Message>            │
└─────────────────────────────────────────────┘
           │ 1
           ▼
┌──────────────────────┐
│       Message        │
├──────────────────────┤
│ + MessageID: int     │
│ + SessionID: int     │
│ + SenderID: int      │
│ + MessageText: string│
│ + SentAt: DateTime   │
└──────────────────────┘

┌──────────────────────────────────────────────┐
│              PointsTransaction               │
├──────────────────────────────────────────────┤
│ + TransactionID: int (PK)                   │
│ + UserID: int (FK)                          │
│ + Amount: int (positive = earn, neg = spend)│
│ + Title: string                             │
│ + CreatedAt: DateTime                       │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│               Notification                  │
├──────────────────────────────────────────────┤
│ + NotificationID: int (PK)                  │
│ + UserID: int (FK)                          │
│ + Type: string                              │
│   ("NewAnswer"|"AnswerAccepted"|             │
│    "PostUpvote"|"AnswerUpvote"|             │
│    "SessionAccepted"|"TutoringRequest")     │
│ + ReferenceID: int (polymorphic FK)         │
│ + IsRead: bool                              │
│ + IsDeleted: bool                           │
│ + CreatedAt: DateTime                       │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│                   Report                    │
├──────────────────────────────────────────────┤
│ + ReportID: int (PK)                        │
│ + ReporterID: int (FK → User)               │
│ + TargetID: int (polymorphic FK)            │
│ + TargetType: string ("Post" | "Answer")    │
│ + Reason: string                            │
│ + CreatedAt: DateTime                       │
└──────────────────────────────────────────────┘
```

### 5.3.2 Key Relationships

| Relationship | Type | Description |
|---|---|---|
| User → Post | 1 to Many | A user can post many questions |
| User → Answer | 1 to Many | A user can post many answers |
| Post → Answer | 1 to Many | A question can have many answers |
| Post ↔ Tag | Many to Many | Via PostTag junction table |
| Request → PrivateSession | 1 to 1 | One accepted request creates one session |
| PrivateSession → Message | 1 to Many | A session contains many messages |
| Post → PostUpvote | 1 to Many | Track which users upvoted which post |
| Answer → AnswerUpvote | 1 to Many | Track which users upvoted which answer |

### 5.3.3 Service Interfaces

```
IPostService
├── CreatePost(viewModel, userId) → Post
├── GetPostById(postId) → Post
├── DeletePost(postId, userId) → bool
├── PostAnswer(postId, content, userId, imageFile) → Answer
├── AcceptAnswer(answerId, userId) → bool  [max 1 per post]
├── UpvotePost(postId, userId) → int
├── UpvoteAnswer(answerId, userId) → int
├── RequestTutoring(postId, userId, description) → Request
├── AcceptTutoring(requestId, helperId) → PrivateSession
└── SaveImage(file, subfolder) → string

IPointService
├── AwardPoints(userId, amount, reason) → void
├── DeductPoints(userId, amount, reason) → bool
├── GetTransactionHistory(userId) → List<PointsTransaction>
└── GetLevel(points) → (level, progressPercent, levelText)
```

---

## 5.4 Database Design

### 5.4.1 Database Technology

| Environment | Database | Connection |
|---|---|---|
| Development | SQLite (`uni-connect-dev.db`) | File-based, zero configuration |
| Production | SQL Server | Trusted Connection (Windows Auth) |

EF Core Code-First migrations manage schema evolution. All entities include `CreatedAt: DateTime` and most include `IsDeleted: bool` for the soft-delete pattern.

### 5.4.2 Entity Relationship Diagram (Textual)

```
USERS
├── UserID (PK)
├── UniversityID (UNIQUE)
├── Email (UNIQUE)
├── Username (UNIQUE)
├── Name, PasswordHash, Role, Faculty, YearOfStudy
├── Points, ProfileImageUrl
├── IsDeleted, CreatedAt
│
├──◄ POSTS (UserID FK)
├──◄ ANSWERS (UserID FK)
├──◄ NOTIFICATIONS (UserID FK)
├──◄ POINTS_TRANSACTIONS (UserID FK)
├──◄ POST_UPVOTES (UserID FK)
├──◄ ANSWER_UPVOTES (UserID FK)
├──◄ REPORTS as Reporter (ReporterID FK)
├──◄ PRIVATE_SESSIONS as Student (StudentID FK)
└──◄ PRIVATE_SESSIONS as Helper (HelperID FK)

POSTS
├── PostID (PK)
├── Title, Content, CourseCode, ImageUrl
├── Upvotes, ViewsCount
├── IsEndorsed, IsDeleted, CreatedAt
├── UserID (FK → USERS)
├── CategoryID (FK → CATEGORIES)
│
├──◄ ANSWERS (PostID FK)
├──◄ POST_TAGS (PostID FK)
├──◄ POST_UPVOTES (PostID FK)
└──◄ REQUESTS (PostID FK)

ANSWERS
├── AnswerID (PK)
├── Content, ImageUrl
├── Upvotes, IsAccepted
├── IsDeleted, CreatedAt
├── PostID (FK → POSTS)
├── UserID (FK → USERS)
└──◄ ANSWER_UPVOTES (AnswerID FK)

CATEGORIES
├── CategoryID (PK)
├── Name
└── Faculty

TAGS
├── TagID (PK)
└── Name (UNIQUE)

POST_TAGS  [Junction]
├── PostID (FK → POSTS)
└── TagID (FK → TAGS)

POST_UPVOTES  [Prevents duplicate upvotes]
├── PostUpvoteID (PK)
├── PostID (FK → POSTS)
└── UserID (FK → USERS)

ANSWER_UPVOTES  [Prevents duplicate upvotes]
├── AnswerUpvoteID (PK)
├── AnswerID (FK → ANSWERS)
└── UserID (FK → USERS)

REQUESTS  [Tutoring request before session opens]
├── RequestID (PK)
├── PostID (FK → POSTS)
├── OwnerID (FK → USERS — the person who asked)
├── Status ("Open" | "Accepted")
├── Description
└── CreatedAt

PRIVATE_SESSIONS
├── PrivateSessionID (PK)
├── RequestID (FK → REQUESTS)
├── StudentID (FK → USERS)
├── HelperID (FK → USERS)
├── IsDeleted, CreatedAt
└──◄ MESSAGES (SessionID FK)

MESSAGES
├── MessageID (PK)
├── SessionID (FK → PRIVATE_SESSIONS)
├── SenderID (FK → USERS)
├── MessageText
└── SentAt

NOTIFICATIONS
├── NotificationID (PK)
├── UserID (FK → USERS)
├── Type (string — see types above)
├── ReferenceID (polymorphic — points to Post/Answer/Session/Request)
├── IsRead, IsDeleted, CreatedAt
└── [Note: ReferenceID is polymorphic based on Type field]

POINTS_TRANSACTIONS
├── TransactionID (PK)
├── UserID (FK → USERS)
├── Amount (positive = earned, negative = spent)
├── Title (description of transaction)
└── CreatedAt

REPORTS
├── ReportID (PK)
├── ReporterID (FK → USERS)
├── TargetID (polymorphic FK — Post or Answer)
├── TargetType ("Post" | "Answer")
├── Reason
└── CreatedAt
```

### 5.4.3 Points System Rules (Stored in PointsTransaction)

| Event | Points | Trigger |
|---|---|---|
| Welcome bonus (registration) | +50 | `LoginController.Register` |
| Post an answer | +5 | `PostService.PostAnswer` |
| Receive a post upvote | +5 | `PostService.UpvotePost` |
| Receive an answer upvote | +10 | `PostService.UpvoteAnswer` |
| Answer accepted as best | +15 | `PostService.AcceptAnswer` |
| Ask a question | −10 | `PostService.CreatePost` |
| Redeem campus reward | −variable | `DashboardController.RedeemReward` |

**Level Formula:** `Level = (Points ÷ 500) + 1`, capped at Level 10.

---

## 5.5 User Interface (UI) Design

### 5.5.1 Design Language

UniConnect uses a custom design system built on:
- **Font:** Plus Jakarta Sans (Google Fonts) — weights 400/500/600/700/800
- **Primary color:** Indigo (#3D52A0)
- **Accent colors:** Violet (#7091E6), Amber (#F59E0B), Emerald (#10B981), Rose (#F43F5E)
- **Background:** Soft blue-white (#F8F9FF)
- **Icons:** Lucide SVG icon library (consistent, clean line icons)
- **Border radius:** 12px (cards), 99px (chips/badges)
- **Shadows:** Multi-layer box-shadow for depth on interactive elements

### 5.5.2 Layout Architecture

```
┌────────────────────────────────────────────────────┐
│  NAVBAR  [Logo | Search Bar | Bell | Avatar Menu]  │
├──────────────────┬─────────────────────────────────┤
│                  │                                  │
│   SIDEBAR        │       MAIN CONTENT              │
│   ──────         │       ────────────              │
│   User Card      │       Page-specific view        │
│   Home Feed      │       (Dashboard, Profile,      │
│   Ask Question   │        SinglePost, Leaderboard, │
│   Sessions       │        Points, Notifications,   │
│   Leaderboard    │        Sessions, Admin)          │
│   Notifications  │                                  │
│   My Profile     │                                  │
│   Points         │                                  │
│   ──────         │                                  │
│   Points Widget  │                                  │
│   Sign Out       │                                  │
│                  │                                  │
└──────────────────┴─────────────────────────────────┘
```

### 5.5.3 Key Pages

**1. Landing Page (`/`)**
- Hero section with animated gradient background
- Live statistics (student count, questions, answers — pulled from DB)
- 6-step "How it works" section
- Feature cards grid (Q&A, Peer Tutoring, Points, Leaderboard, Moderation)
- Campus venue rewards showcase (6 venues)
- Call-to-action buttons for Register/Login

**2. Dashboard (Main Feed) (`/Dashboard`)**
- 3 filter tabs: All Questions | My Faculty | Trending
- Faculty filter pills (IT, Engineering, Business, Law, Pharmacy)
- Question cards with: author avatar, title, preview text, category chip, stats (upvotes/answers/views)
- Upvote directly from feed (AJAX, no page reload)
- Trending Topics sidebar (top 5 by views + upvotes)
- Community Stats widget (total questions + user points)

**3. Ask a Question (`/Dashboard/CreatePost`)**
- 3-step wizard: Search First → Write Question → Preview & Post
- Step 1: Live search against real DB (debounced 350ms)
- Step 2: Title, Faculty, Course Code, Description, Tags (autocomplete dropdown), Image upload
- Step 3: Preview card showing exactly how it will look, pre-post checklist
- Step 4: Success screen with earn-back information

**4. Single Post (`/Dashboard/SinglePost/{id}`)**
- Full question display with author, date, category, course code
- Upvote button (pill style, goes amber on click)
- Sorted answers: accepted first, then by upvotes
- Per-answer upvote button (vertical chip, amber on `.voted` state)
- Post author can accept best answer (auto-upvotes it)
- Answer submission form with image upload
- Peer Tutoring request panel (for non-authors)

**5. Profile (`/Dashboard/Profile`)**
- Cover banner with avatar, name, faculty badges
- 4-stat grid: Posts, Answers, Points, Reputation
- Level progress bar toward Verified Tutor (1000 pts)
- Tabs: Questions posted | Answers given | Settings (edit name, bio, avatar)

**6. Leaderboard (`/Dashboard/Leaderboard`)**
- Podium display for top 3 users (gold/silver/bronze)
- Full rankings table with points, level, answer count
- User's personal rank banner at the top

**7. Points & Rewards (`/Dashboard/Points`)**
- Hero card showing current balance and level progress
- 4 tabs: Redeem Venues | How to Earn | Transactions | Achievements
- 6 campus venue cards (Cafeteria, Library, Bookshop, Print Center, Canteen, Sports)
- Points-based unlock (locked venues shown with lock overlay)
- Transaction history with timestamps

**8. Admin Dashboard (`/Admin/AdminDashboard`)**
- 4 KPI stat cards: Students, Active Questions, Solutions, Pending Reports
- Faculty participation bar chart
- Recent reports feed with quick Dismiss/Delete actions
- Links to full Manage Users and Manage Reports pages

### 5.5.4 Usability Principles Applied

| Principle | Implementation |
|---|---|
| Feedback | Toast notifications for every action (upvote, post, redeem) |
| Error prevention | Wizard with validation before proceeding, checklist before posting |
| Recognition over recall | Icon + text labels on all sidebar navigation items |
| Consistency | Single design system CSS used across all pages |
| Accessibility | Semantic HTML, focus states on form inputs, alt text on avatars |
| Mobile responsiveness | Collapsible sidebar, floating action button, hide-scrollbar utilities |

---

## 5.6 Summary

The UniConnect system design establishes a robust foundation for a university-wide peer-learning platform through the following decisions:

**Efficiency** — The MVC layered architecture ensures clean separation of concerns. Business logic in `PostService` and `PointService` is reusable across multiple controllers. EF Core global query filters eliminate the need for redundant `WHERE IsDeleted = false` clauses across every query.

**Security** — Multiple defense layers protect user data: BCrypt hashing prevents password exposure, anti-CSRF tokens block cross-site request forgery, role-based guards prevent privilege escalation, and session membership verification prevents private message leakage.

**Scalability** — The database schema supports growth through proper normalization. The `PostUpvote` and `AnswerUpvote` tables prevent duplicate voting while maintaining a clean audit trail. `PointsTransaction` provides a full financial ledger that scales without modifying the `User.Points` column directly.

**Maintainability** — Service interfaces (`IPostService`, `IPointService`) allow future implementation swaps without controller changes. The migration-based database management means schema changes are tracked, versioned, and repeatable.

**Future Roadmap** — The architecture was specifically designed to accommodate these planned enhancements without structural refactoring:
1. SMTP email delivery for password reset and notifications
2. QR Code scanning for physical campus reward redemption
3. AI-assisted content moderation (sits between Report submission and Admin review)
4. React Native mobile app (DashboardController endpoints already return JSON for AJAX)
5. Daily login streak rewards (+2 pts system already has PointsTransaction infrastructure)
6. Session rating and completion flow
7. Registration email verification before account activation

This design chapter serves as the complete blueprint guiding the implementation phase (Task 6, due April 25, 2026) and the final project defense.

---

*UniConnect System Design — Chapter 5*
*Philadelphia University | Group 2 — Training For Students*
*Document prepared: April 2026*
