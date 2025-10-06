# CineReview CMS

A modern React 18 + TypeScript control center for managing CineReview identity and content platforms. Built with Vite, Material UI, React Router, and TanStack Query to support future expansion to additional partner sites.

## Getting started

```bash
npm install
npm run dev
```

The app reads API endpoints from environment variables. Copy `.env.example` to `.env` and adjust the base URLs as needed.

```bash
cp .env.example .env
```

## Available scripts

- `npm run dev` – start the Vite development server
- `npm run build` – type-check and produce a production bundle
- `npm run preview` – preview the production build locally
- `npm run lint` – run ESLint across the project

## Architecture highlights

- **UI & Theming** – Material UI 7 with a custom light/dark palette persisted in `localStorage`.
- **Data fetching** – Axios clients with interceptors wired through TanStack Query for caching and request lifecycle handling.
- **Routing** – React Router v7 with guarded dashboards that respect Identity roles (Administrator & Partner).
- **State management** – `ApplicationContext` centralizes theme, platform selection, and authentication state.
- **Features** –
	- Identity users: pagination, banning, and role assignment.
	- Identity roles: CRUD with confirmation flows.
	- CineReview tags: filtering, CRUD, and status management.
	- CineReview reviews: filtering, detail inspection, and score recalculation.

## Project structure

```
src/
	components/        // Reusable UI primitives (navigation, tables, feedback)
	constants/         // Shared constants (storage keys, roles)
	contexts/          // Application-level context providers
	features/          // Domain-specific modules (auth, identity, cine-review)
	layouts/           // Layout shells such as the dashboard frame
	providers/         // Global providers (React Query, Snackbar)
	routes/            // Router configuration and guard components
	services/          // Axios instances and API helpers
	theme/             // Material UI theme factory
	utils/             // Utility helpers (storage, service response parsing)
```

## Authentication

Sign-in supports Google OAuth redirection via the Identity API or direct credential assertions against `/api/account/client-authenticate`. After successful authentication the CMS enforces access to Administrator and Partner roles only.

## Platform expansion

`ApplicationContext` manages a list of platforms; swapping the active platform updates Axios base URLs. Additional platform definitions can be added to the `DEFAULT_PLATFORMS` collection to scale beyond CineReview.
