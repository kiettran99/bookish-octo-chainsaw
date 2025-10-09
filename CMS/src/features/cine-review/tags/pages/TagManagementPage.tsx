import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  Box,
  Button,
  Chip,
  IconButton,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
} from '@mui/material'
import { DataGrid, type GridColDef } from '@mui/x-data-grid'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import DeleteRoundedIcon from '@mui/icons-material/DeleteRounded'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { enqueueSnackbar } from 'notistack'

import { PageContainer } from '@/components/layout/PageContainer'
import { ConfirmDialog } from '@/components/feedback/ConfirmDialog'
import { StandardGridToolbar } from '@/components/tables/StandardGridToolbar'
import { TagDialog } from '@/features/cine-review/tags/components/TagDialog'
import {
  TAG_CATEGORY_LABELS,
  type CreateTagRequestModel,
  type TagCategory,
  type TagResponseModel,
  type UpdateTagRequestModel,
} from '@/features/cine-review/tags/types'
import { createTag, deleteTag, fetchTags, updateTag } from '@/features/cine-review/tags/services/tags.api'

export function TagManagementPage() {
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all')
  const [categoryFilter, setCategoryFilter] = useState<TagCategory | 'all'>('all')
  const [searchTerm, setSearchTerm] = useState<string>('')
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [selectedTag, setSelectedTag] = useState<TagResponseModel | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const queryClient = useQueryClient()

  const tagsQuery = useQuery({
    queryKey: ['tags', statusFilter, categoryFilter],
    queryFn: () =>
      fetchTags({
        isActive: statusFilter === 'active' ? true : statusFilter === 'inactive' ? false : undefined,
        category: categoryFilter === 'all' ? undefined : categoryFilter,
      }),
  })

  // Filter tags by search term on client side
  const filteredTags = useMemo(() => {
    const tags = tagsQuery.data ?? []
    if (!searchTerm) return tags
    
    const search = searchTerm.toLowerCase()
    return tags.filter((tag) => 
      tag.name.toLowerCase().includes(search) ||
      tag.categoryName.toLowerCase().includes(search)
    )
  }, [tagsQuery.data, searchTerm])

  const createMutation = useMutation({
    mutationFn: createTag,
    onSuccess: () => {
      enqueueSnackbar('Tag created successfully', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['tags'] })
      setDialogOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to create tag'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const updateMutation = useMutation({
    mutationFn: updateTag,
    onSuccess: () => {
      enqueueSnackbar('Tag updated successfully', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['tags'] })
      setDialogOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to update tag'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteTag,
    onSuccess: () => {
      enqueueSnackbar('Tag deleted successfully', { variant: 'success' })
      queryClient.invalidateQueries({ queryKey: ['tags'] })
      setConfirmOpen(false)
    },
    onError: (error) => {
      const message = error instanceof Error ? error.message : 'Failed to delete tag'
      enqueueSnackbar(message, { variant: 'error' })
    },
  })

  const handleOpenCreate = () => {
    setDialogMode('create')
    setSelectedTag(null)
    setDialogOpen(true)
  }

  const handleOpenEdit = (tag: TagResponseModel) => {
    setDialogMode('edit')
    setSelectedTag(tag)
    setDialogOpen(true)
  }

  const handleDelete = (tag: TagResponseModel) => {
    setSelectedTag(tag)
    setConfirmOpen(true)
  }

  const columns = useMemo<GridColDef<TagResponseModel>[]>(
    () => [
      {
        field: 'name',
        headerName: 'Name',
        flex: 1,
        minWidth: 200,
      },
      {
        field: 'categoryName',
        headerName: 'Category',
        flex: 1,
        minWidth: 160,
      },
      {
        field: 'isActive',
        headerName: 'Status',
        minWidth: 140,
        renderCell: ({ value }) => (
          <Chip label={value ? 'Active' : 'Inactive'} color={value ? 'success' : 'default'} size="small" />
        ),
      },
      {
        field: 'displayOrder',
        headerName: 'Order',
        width: 110,
      },
      {
        field: 'actions',
        headerName: 'Actions',
        width: 140,
        sortable: false,
        filterable: false,
        renderCell: ({ row }) => (
          <Stack direction="row" spacing={1}>
            <IconButtonWithTooltip title="Edit" onClick={() => handleOpenEdit(row)}>
              <EditRoundedIcon fontSize="small" />
            </IconButtonWithTooltip>
            <IconButtonWithTooltip title="Delete" color="error" onClick={() => handleDelete(row)}>
              <DeleteRoundedIcon fontSize="small" />
            </IconButtonWithTooltip>
          </Stack>
        ),
      },
    ],
    [],
  )

  const rows = filteredTags.map((tag) => ({ ...tag, id: tag.id }))

  return (
    <PageContainer
      title="Tags"
      subtitle="Curate experience facets that power reviewer storytelling and discovery."
      action={
        <Stack direction="row" spacing={1}>
          <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={handleOpenCreate}>
            New tag
          </Button>
        </Stack>
      }
    >
      <Stack spacing={2} mb={2}>
        <TextField
          label="Search by name or category"
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
          size="small"
          placeholder="Type to filter tags..."
        />
        
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ xs: 'stretch', sm: 'center' }}>
          <ToggleButtonGroup
            value={statusFilter}
            exclusive
            onChange={(_, value) => value && setStatusFilter(value)}
            size="small"
          >
            <ToggleButton value="all">All</ToggleButton>
            <ToggleButton value="active">Active</ToggleButton>
            <ToggleButton value="inactive">Inactive</ToggleButton>
          </ToggleButtonGroup>

          <ToggleButtonGroup
            value={categoryFilter}
            exclusive
            onChange={(_, value) => {
              if (value === null) return
              setCategoryFilter(value as TagCategory | 'all')
            }}
            size="small"
          >
            <ToggleButton value="all">All categories</ToggleButton>
            {Object.entries(TAG_CATEGORY_LABELS).map(([key, label]) => (
              <ToggleButton key={key} value={Number(key)}>
                {label}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </Stack>
      </Stack>

      <Box sx={{ height: 560 }}>
        <DataGrid
          rows={rows}
          columns={columns}
          loading={tagsQuery.isLoading || tagsQuery.isRefetching}
          disableRowSelectionOnClick
          slots={{ toolbar: StandardGridToolbar }}
        />
      </Box>

      <TagDialog
        open={dialogOpen}
        mode={dialogMode}
        tag={selectedTag}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
        onClose={() => setDialogOpen(false)}
        onSubmit={(payload) => {
          if (dialogMode === 'create') {
            createMutation.mutate(payload as CreateTagRequestModel)
          } else {
            updateMutation.mutate(payload as UpdateTagRequestModel)
          }
        }}
      />

      <ConfirmDialog
        open={confirmOpen}
        title="Delete tag"
        content={`This will archive the tag "${selectedTag?.name}" for all experiences.`}
        isLoading={deleteMutation.isPending}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => {
          if (selectedTag) {
            deleteMutation.mutate(selectedTag.id)
          }
        }}
      />
    </PageContainer>
  )
}

interface IconButtonWithTooltipProps {
  title: string
  children: ReactNode
  onClick: () => void
  color?: 'inherit' | 'default' | 'primary' | 'secondary' | 'success' | 'error' | 'info' | 'warning'
}

function IconButtonWithTooltip({ title, children, onClick, color = 'default' }: IconButtonWithTooltipProps) {
  return (
    <Tooltip title={title}>
      <IconButton size="small" color={color} onClick={onClick}>
        {children}
      </IconButton>
    </Tooltip>
  )
}
