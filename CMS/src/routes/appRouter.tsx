import { createBrowserRouter, Navigate } from 'react-router-dom'

import { ProtectedRoute } from '@/routes/components/ProtectedRoute'
import { DashboardLayout } from '@/layouts/dashboard/DashboardLayout'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { OverviewPage } from '@/features/dashboard/pages/OverviewPage'
import { UserListPage } from '@/features/identity/users/pages/UserListPage'
import { RoleManagementPage } from '@/features/identity/roles/pages/RoleManagementPage'
import { TagManagementPage } from '@/features/cine-review/tags/pages/TagManagementPage'
import { ReviewManagementPage } from '@/features/cine-review/reviews/pages/ReviewManagementPage'
import { AppRoot } from '@/routes/components/AppRoot'

export const appRouter = createBrowserRouter([
  {
    element: <AppRoot />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <ProtectedRoute />,
        children: [
          {
            path: '/',
            element: <DashboardLayout />,
            children: [
              { index: true, element: <OverviewPage /> },
              { path: 'identity/users', element: <UserListPage /> },
              { path: 'identity/roles', element: <RoleManagementPage /> },
              { path: 'cine-review/tags', element: <TagManagementPage /> },
              { path: 'cine-review/reviews', element: <ReviewManagementPage /> },
            ],
          },
        ],
      },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
])
