import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import {
  Box,
  Drawer,
  Toolbar,
  useMediaQuery,
  useTheme,
  Divider,
  Paper,
} from '@mui/material'

import { TopBar } from '@/components/navigation/TopBar'
import { SidebarNav } from '@/components/navigation/SidebarNav'
import { NAV_ITEMS } from '@/components/navigation/navConfig'

const DRAWER_WIDTH = 280

export function DashboardLayout() {
  const theme = useTheme()
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'))
  const [mobileOpen, setMobileOpen] = useState(false)

  const handleDrawerToggle = () => {
    setMobileOpen((prev) => !prev)
  }

  const drawer = (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <SidebarNav items={NAV_ITEMS} onNavigate={() => setMobileOpen(false)} />
      <Box sx={{ flexGrow: 1 }} />
      <Divider />
      <Box sx={{ p: 2 }}>
        <Paper variant="outlined" sx={{ p: 2, borderRadius: 3 }}>
          Manage content & identify trusted reviewers across CineReview platforms.
        </Paper>
      </Box>
    </Box>
  )

  return (
    <Box sx={{ display: 'flex' }}>
      <TopBar onOpenSidebar={handleDrawerToggle} />
      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant={isDesktop ? 'permanent' : 'temporary'}
          open={isDesktop ? true : mobileOpen}
          onClose={handleDrawerToggle}
          ModalProps={{ keepMounted: true }}
          sx={{
            '& .MuiDrawer-paper': {
              width: DRAWER_WIDTH,
              borderRight: `1px solid ${theme.palette.divider}`,
              bgcolor: theme.palette.background.paper,
            },
          }}
        >
          {drawer}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          px: { xs: 2, md: 4 },
          minHeight: '100vh',
          bgcolor: (t) => (t.palette.mode === 'light' ? '#f3f5fb' : '#0b1222'),
        }}
      >
        <Toolbar sx={{ mb: 4 }} />
        <Outlet />
      </Box>
    </Box>
  )
}
