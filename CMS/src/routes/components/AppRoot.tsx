import { Outlet } from 'react-router-dom'

import { ApplicationProvider } from '@/contexts/ApplicationContext'

export function AppRoot() {
  return (
    <ApplicationProvider>
      <Outlet />
    </ApplicationProvider>
  )
}
