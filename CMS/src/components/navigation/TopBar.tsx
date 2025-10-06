import { useState } from 'react'
import MenuRoundedIcon from '@mui/icons-material/MenuRounded'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import LightModeRoundedIcon from '@mui/icons-material/LightModeRounded'
import DarkModeRoundedIcon from '@mui/icons-material/DarkModeRounded'
import {
  AppBar,
  Avatar,
  Box,
  IconButton,
  Menu,
  MenuItem,
  Select,
  Toolbar,
  Tooltip,
  Typography,
  useTheme,
} from '@mui/material'

import { useApplicationContext } from '@/contexts/ApplicationContext'
import { useAuth } from '@/hooks/useAuth'

interface TopBarProps {
  onOpenSidebar?: () => void
}

export function TopBar({ onOpenSidebar }: TopBarProps) {
  const { themeMode, toggleThemeMode, platforms, currentPlatform, setPlatform } = useApplicationContext()
  const { auth, logout } = useAuth()
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null)
  const theme = useTheme()

  const open = Boolean(anchorEl)

  const handleAvatarClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget)
  }

  const handleClose = () => setAnchorEl(null)

  const userInitial = auth.user?.fullName?.charAt(0)?.toUpperCase() ?? 'C'

  return (
    <AppBar
      position="fixed"
      elevation={0}
      sx={{
        backdropFilter: 'blur(10px)',
        backgroundColor: theme.palette.mode === 'light' ? 'rgba(255,255,255,0.85)' : 'rgba(17,27,49,0.85)',
        borderBottom: (t) => `1px solid ${t.palette.divider}`,
      }}
    >
      <Toolbar sx={{ minHeight: 72, px: { xs: 2, sm: 3 } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexGrow: 1 }}>
          <IconButton
            edge="start"
            color="inherit"
            onClick={onOpenSidebar}
            sx={{ display: { md: 'none', xs: 'inline-flex' } }}
          >
            <MenuRoundedIcon />
          </IconButton>
          <Box sx={{ display: 'flex', flexDirection: 'column' }}>
            <Typography variant="subtitle2" color="text.secondary">
              Welcome back
            </Typography>
            <Typography variant="h6" fontWeight={700} color="text.primary">
              {auth.user?.fullName ?? 'Administrator'}
            </Typography>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Select
            size="small"
            value={currentPlatform.id}
            onChange={(event) => setPlatform(event.target.value)}
            sx={{
              minWidth: 160,
              borderRadius: 3,
              '& .MuiSelect-select': {
                display: 'flex',
                alignItems: 'center',
                gap: 1,
              },
            }}
          >
            {platforms.map((platform) => (
              <MenuItem key={platform.id} value={platform.id}>
                {platform.label}
              </MenuItem>
            ))}
          </Select>

          <Tooltip title={themeMode === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}>
            <IconButton onClick={toggleThemeMode} color="primary" sx={{ borderRadius: 2 }}>
              {themeMode === 'dark' ? <LightModeRoundedIcon /> : <DarkModeRoundedIcon />}
            </IconButton>
          </Tooltip>

          <Tooltip title="Account options">
            <Avatar
              sx={{ cursor: 'pointer' }}
              onClick={handleAvatarClick}
              src={auth.user?.avatar ?? undefined}
            >
              {userInitial}
            </Avatar>
          </Tooltip>
          <Menu anchorEl={anchorEl} open={open} onClose={handleClose} PaperProps={{ sx: { minWidth: 220 } }}>
            <Box sx={{ px: 2, py: 1.5 }}>
              <Typography variant="subtitle2" fontWeight={700}>
                {auth.user?.fullName ?? 'Administrator'}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {auth.user?.email}
              </Typography>
            </Box>
            <MenuItem
              onClick={() => {
                handleClose()
                logout()
              }}
            >
              <LogoutRoundedIcon fontSize="small" sx={{ mr: 1 }} /> Sign out
            </MenuItem>
          </Menu>
        </Box>
      </Toolbar>
    </AppBar>
  )
}
