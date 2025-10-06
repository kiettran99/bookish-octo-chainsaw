import { RouterProvider } from 'react-router-dom'

import { AppProviders } from './providers/AppProviders'
import { appRouter } from './routes/appRouter'

export default function App() {
  return (
    <AppProviders>
      <RouterProvider router={appRouter} />
    </AppProviders>
  )
}
