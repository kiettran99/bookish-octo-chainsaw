import { useEffect, useMemo, useState } from 'react'
import {
  Avatar,
  Box,
  Button,
  Chip,
  Divider,
  Drawer,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'

import type { RoleModel } from '@/features/identity/roles/types'
import type { UserPagingModel, UserUpdateRequestModel } from '@/features/identity/users/types'

interface UserDetailDrawerProps {
  open: boolean
  user: UserPagingModel | null
  availableRoles: RoleModel[]
  isSubmitting: boolean
  onClose: () => void
  onSubmit: (payload: UserUpdateRequestModel) => void
}

const defaultForm: UserUpdateRequestModel = {
  fullName: '',
  roles: [],
  isBanned: false,
  region: '',
}

export function UserDetailDrawer({
  open,
  user,
  availableRoles,
  isSubmitting,
  onClose,
  onSubmit,
}: UserDetailDrawerProps) {
  const [form, setForm] = useState<UserUpdateRequestModel>(defaultForm)

  useEffect(() => {
    if (!user) {
      setForm(defaultForm)
      return
    }

    const parsedRoles = user.roles
      ?.split(',')
      .map((role) => role.trim())
      .filter(Boolean)

    setForm({
      fullName: user.fullName ?? '',
      roles: parsedRoles ?? [],
      isBanned: user.isBanned,
      region: user.region ?? '',
    })
  }, [user])

  const roleOptions = useMemo(() => availableRoles.map((role) => role.name), [availableRoles])

  const handleChange = (field: keyof UserUpdateRequestModel) =>
    (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      if (field === 'roles') {
        const { value } = event.target as HTMLInputElement
        const selected = typeof value === 'string' ? value.split(',') : (value as string[])
        setForm((prev) => ({ ...prev, roles: selected }))
        return
      }

      setForm((prev) => ({ ...prev, [field]: event.target.value }))
    }

  const handleToggleBan = (_: React.ChangeEvent<HTMLInputElement>, checked: boolean) => {
    setForm((prev) => ({ ...prev, isBanned: checked }))
  }

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    onSubmit(form)
  }

  const disabled = !user

  return (
    <Drawer anchor="right" open={open} onClose={onClose} PaperProps={{ sx: { width: 420 } }}>
      <Box component="form" onSubmit={handleSubmit} sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
        <Box sx={{ px: 3, py: 3 }}>
          <Stack direction="row" spacing={2} alignItems="center">
            <Avatar src={user?.avatar ?? undefined} sx={{ width: 64, height: 64 }}>
              {user?.fullName?.[0]?.toUpperCase() ?? '?'}
            </Avatar>
            <Box>
              <Typography variant="h6" fontWeight={700}>
                {user?.fullName ?? 'User details'}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {user?.email}
              </Typography>
            </Box>
          </Stack>
        </Box>

        <Divider />

        <Stack spacing={2.5} sx={{ px: 3, py: 3, flexGrow: 1, overflowY: 'auto' }}>
          <TextField
            required
            label="Full name"
            value={form.fullName}
            onChange={handleChange('fullName')}
            disabled={disabled}
          />

          <TextField
            label="Region"
            value={form.region ?? ''}
            onChange={handleChange('region')}
            disabled={disabled}
          />

          <FormControl disabled={disabled}>
            <InputLabel id="user-roles-label">Roles</InputLabel>
            <Select
              labelId="user-roles-label"
              label="Roles"
              multiple
              value={form.roles ?? []}
              onChange={(event) => {
                const value = event.target.value
                const selected = typeof value === 'string' ? value.split(',') : value
                setForm((prev) => ({ ...prev, roles: selected as string[] }))
              }}
              renderValue={(selected) => (
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
                  {(selected as string[]).map((role) => (
                    <Chip key={role} label={role} size="small" />
                  ))}
                </Box>
              )}
            >
              {roleOptions.map((role) => (
                <MenuItem key={role} value={role}>
                  {role}
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>Assign access levels available to this platform.</FormHelperText>
          </FormControl>

          <FormControl disabled={disabled}>
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <Box>
                <Typography variant="subtitle1" fontWeight={600}>
                  Banned status
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Banned users cannot access CineReview experiences.
                </Typography>
              </Box>
              <Switch checked={!!form.isBanned} onChange={handleToggleBan} />
            </Stack>
          </FormControl>

          <Box>
            <Typography variant="subtitle2" color="text.secondary">
              Username
            </Typography>
            <Typography variant="body1">{user?.userName}</Typography>
          </Box>

          <Box>
            <Typography variant="subtitle2" color="text.secondary">
              Created
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {user?.createdOnUtc ? new Date(user.createdOnUtc).toLocaleString() : '—'}
            </Typography>
          </Box>
        </Stack>

        <Divider />

        <Stack direction="row" spacing={2} sx={{ px: 3, py: 2 }}>
          <Button fullWidth variant="outlined" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" fullWidth variant="contained" disabled={disabled || isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Save changes'}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  )
}
