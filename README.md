🎫 Eventualno

A full-stack web application that recommends local events to users based on their past interactions and preferences — powered by ASP.NET Core 8 MVC, Entity Framework Core, ML.NET, and a modern React + Vite + Tailwind frontend.

🧩 Overview

The project is a complete event discovery platform that allows users to:

Browse and explore upcoming events by category, location, or popularity.

Interact with events (like, rate, attend) to personalize recommendations.

Receive weekly digest emails with tailored event picks.

Manage their profile and see personalized analytics.

It consists of two main parts:

EventRecommender/
 ├── EventRecommender/     → ASP.NET Core 8 backend (API, MVC, ML, Identity)
 └── client/               → React frontend (Vite + Tailwind CSS)

🖥️ Backend (ASP.NET Core 8 MVC)

Tech stack:

ASP.NET Core 8 MVC — web framework & API layer

Entity Framework Core 8 — data access and migrations

Identity — authentication & user management

ML.NET — recommendation engine (Matrix Factorization + FastTreeRanking)

SQLite / SQL Server — persistence layer

Hosted Services — weekly digest background mailer

Dependency Injection & Repository / Unit of Work patterns

Main features:

RESTful API endpoints (/api/events, /api/auth, /api/recommendations)

ML model training and evaluation dashboard (/Admin/Train, /Admin/Metrics)

Background service (WeeklyDigestService) that sends personalized email digests

Developer endpoint (/api/dev/digest-me-now) to test email rendering

💌 Weekly Digest Emails

Each Monday, users automatically receive a rich HTML email listing their top 6 recommended events.
The email is rendered with inline styles for full compatibility (tested on Gmail, Outlook, and mobile).

You can trigger it manually in development via:

curl -k -L -i -b cookies.txt -X POST https://localhost:5210/api/dev/digest-me-now

🌐 Frontend (React + Vite + Tailwind)

Tech stack:

React 18 + TypeScript

Vite for ultra-fast dev builds

Tailwind CSS with a custom dark navy “glass” design

Lucide Icons, Shadcn-UI components

Axios API layer

Key components:

EventCard, RatingStars, CategoryChip, EmptyState

Persistent dark-theme UI

Logged-in user context synced via ASP.NET Identity

Dynamic routing (React Router v6)

Run the frontend separately from the backend using:

npm run dev


(Default port: http://localhost:8080)

⚙️ Setup & Run
Prerequisites

.NET 8 SDK

Node.js 18+

(Optional) Visual Studio 2022 / VS Code

Backend
cd EventRecommender/EventRecommender
dotnet restore
dotnet ef database update
dotnet run


Backend runs at: https://localhost:5210

Frontend
cd client
npm install
npm run dev


Frontend runs at: http://localhost:8080

Configuration

Environment variables and app settings are defined in:

appsettings.json

.env for frontend (e.g. VITE_API_BASE=https://localhost:5210)

🧠 Machine Learning

The recommendation system uses a hybrid approach:

Matrix Factorization — user–event collaborative filtering

FastTreeRanking — ranking refinement based on user feedback
Model training and evaluation are available via the /Admin/Train and /Admin/Metrics endpoints.

🧾 Folder Structure
EventRecommender/
 ├── Controllers/          → MVC & API controllers
 ├── Data/                 → DbContext, seeding
 ├── ML/                   → model training & evaluation
 ├── Models/               → entities (Event, User, Category, etc.)
 ├── Services/             → Email, WeeklyDigest, Recommender, etc.
 ├── Views/                → Razor pages for admin / diagnostics
 ├── client/               → React frontend
 └── README.md

 🪶 License

This project is licensed under the MIT License — you are free to use, modify, and distribute with attribution.
