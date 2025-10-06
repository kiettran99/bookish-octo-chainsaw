import { List, ListItem, ListItemButton, ListItemIcon, ListItemText, Typography, Box } from '@mui/material'
import { NavLink, useLocation } from 'react-router-dom'

import type { NavItem } from '@/components/navigation/navConfig'

interface SidebarNavProps {
  items: NavItem[]
  onNavigate?: () => void
}

export function SidebarNav({ items, onNavigate }: SidebarNavProps) {
  const location = useLocation()

  return (
    <Box sx={{ px: 2 }}>
      <Box sx={{ py: 3, px: 1 }}>
        <Typography variant="h5" fontWeight={700} gutterBottom>
          CineReview CMS
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Manage identity and content experiences across CineReview platforms.
        </Typography>
      </Box>
      <List sx={{ mt: 1 }}>
        {items.map((item) => {
          const isActive = location.pathname === item.path

          return (
            <ListItem key={item.path} disablePadding sx={{ mb: 0.5 }}>
              <ListItemButton
                component={NavLink}
                to={item.path}
                onClick={onNavigate}
                selected={isActive}
                sx={{
                  borderRadius: 2,
                  '&.Mui-selected': {
                    bgcolor: (theme) => theme.palette.action.selected,
                  },
                  '&:hover': {
                    bgcolor: (theme) => theme.palette.action.hover,
                  },
                }}
              >
                <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
                <ListItemText
                  primary={item.label}
                  primaryTypographyProps={{ fontWeight: isActive ? 600 : 500 }}
                />
              </ListItemButton>
            </ListItem>
          )
        })}
      </List>
    </Box>
  )
}
