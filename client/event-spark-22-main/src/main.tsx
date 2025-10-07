import React from "react";
import { createRoot } from "react-dom/client";
import { HashRouter, Routes, Route } from "react-router-dom";
import AppShell from "@/layouts/AppShell";
import App from "@/App";
import EventDetails from "@/pages/EventDetails";
import LoginPage from "@/pages/LoginPage";
import RegisterPage from "@/pages/RegisterPage";
import PeoplePage from "@/pages/PeoplePage";
import RecsPage from "@/pages/RecsPage";
import TrendingPage from "@/pages/TrendingPage";
import SavedPage from "@/pages/SavedPage";
import SearchPage from "@/pages/SearchPage"; // if you created it
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <HashRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<App />} />
          <Route path="/event/:id" element={<EventDetails />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/people" element={<PeoplePage />} />
          <Route path="/recs" element={<RecsPage />} />
          <Route path="/trending" element={<TrendingPage />} />
          <Route path="/saved/:mode" element={<SavedPage />} />
          <Route path="/search" element={<SearchPage />} /> {/* optional */}
        </Route>
      </Routes>
    </HashRouter>
  </React.StrictMode>
);
