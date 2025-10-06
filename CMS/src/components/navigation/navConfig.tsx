import type { ReactElement } from 'react'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined'
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined'
import LocalOfferOutlinedIcon from '@mui/icons-material/LocalOfferOutlined'
import ReviewsOutlinedIcon from '@mui/icons-material/ReviewsOutlined'

export interface NavItem {
  label: string
  path: string
  icon: ReactElement
}

export const NAV_ITEMS: NavItem[] = [
  {
    label: 'Overview',
    path: '/',
    icon: <DashboardOutlinedIcon fontSize="small" />,
  },
  {
    label: 'Users',
    path: '/identity/users',
    icon: <GroupOutlinedIcon fontSize="small" />,
  },
  {
    label: 'Roles',
    path: '/identity/roles',
    icon: <ShieldOutlinedIcon fontSize="small" />,
  },
  {
    label: 'Tags',
    path: '/cine-review/tags',
    icon: <LocalOfferOutlinedIcon fontSize="small" />,
  },
  {
    label: 'Reviews',
    path: '/cine-review/reviews',
    icon: <ReviewsOutlinedIcon fontSize="small" />,
  },
]
