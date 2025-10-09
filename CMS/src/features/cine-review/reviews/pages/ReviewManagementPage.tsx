import { useMemo, useState } from 'react'
import {
    Box,
    Chip,
    Stack,
    TextField,
    ToggleButton,
    ToggleButtonGroup,
} from '@mui/material'
import { DataGrid, type GridColDef } from '@mui/x-data-grid'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import DeleteRoundedIcon from '@mui/icons-material/DeleteRounded'
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import IconButton from '@mui/material/IconButton'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { enqueueSnackbar } from 'notistack'

import { PageContainer } from '@/components/layout/PageContainer'
import { StandardGridToolbar } from '@/components/tables/StandardGridToolbar'
import { ConfirmDialog } from '@/components/feedback/ConfirmDialog'
import {
    REVIEW_STATUS,
    REVIEW_STATUS_LABELS,
    REVIEW_TYPE,
    type ReviewResponseModel,
    type ReviewStatus,
    type ReviewType,
} from '@/features/cine-review/reviews/types'
import { approveReview, deleteReview, fetchReviews } from '@/features/cine-review/reviews/services/reviews.api'
import { ReviewDetailDialog } from '@/features/cine-review/reviews/components/ReviewDetailDialog'

export function ReviewManagementPage() {
    const [statusFilter, setStatusFilter] = useState<ReviewStatus | 'all'>('all')
    const [typeFilter, setTypeFilter] = useState<ReviewType | 'all'>(REVIEW_TYPE.Normal)
    const [emailFilter, setEmailFilter] = useState<string>('')
    const [movieFilter, setMovieFilter] = useState<string>('')
    const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: 25 })
    const [selectedReview, setSelectedReview] = useState<ReviewResponseModel | null>(null)
    const [detailOpen, setDetailOpen] = useState(false)
    const [confirmOpen, setConfirmOpen] = useState(false)
    const [confirmAction, setConfirmAction] = useState<'delete' | 'approve'>('delete')
    const queryClient = useQueryClient()

    // Date filters: default to 60 days ago to today
    const getDefaultDateFrom = () => {
        const date = new Date()
        date.setDate(date.getDate() - 60)
        return date.toISOString().split('T')[0]
    }
    
    const getDefaultDateTo = () => {
        return new Date().toISOString().split('T')[0]
    }

    const [dateFrom, setDateFrom] = useState<string>(getDefaultDateFrom())
    const [dateTo, setDateTo] = useState<string>(getDefaultDateTo())

    const reviewsQuery = useQuery({
        queryKey: ['reviews', statusFilter, typeFilter, emailFilter, movieFilter, dateFrom, dateTo, paginationModel],
        queryFn: () =>
            fetchReviews({
                status: statusFilter === 'all' ? undefined : statusFilter,
                type: typeFilter === 'all' ? undefined : typeFilter,
                email: emailFilter || undefined,
                tmdbMovieId: movieFilter ? Number(movieFilter) : undefined,
                dateFrom: dateFrom || undefined,
                dateTo: dateTo || undefined,
                page: paginationModel.page + 1,
                pageSize: paginationModel.pageSize,
            }),
    })

    const approveMutation = useMutation({
        mutationFn: approveReview,
        onSuccess: () => {
            enqueueSnackbar('Review approved successfully', { variant: 'success' })
            queryClient.invalidateQueries({ queryKey: ['reviews'] })
            setConfirmOpen(false)
        },
        onError: (error) => {
            const message = error instanceof Error ? error.message : 'Failed to approve review'
            enqueueSnackbar(message, { variant: 'error' })
        },
    })

    const deleteMutation = useMutation({
        mutationFn: deleteReview,
        onSuccess: () => {
            enqueueSnackbar('Review deleted successfully', { variant: 'success' })
            queryClient.invalidateQueries({ queryKey: ['reviews'] })
            setConfirmOpen(false)
        },
        onError: (error) => {
            const message = error instanceof Error ? error.message : 'Failed to delete review'
            enqueueSnackbar(message, { variant: 'error' })
        },
    })

    const handleAction = (review: ReviewResponseModel, action: 'delete' | 'approve') => {
        setSelectedReview(review)
        setConfirmAction(action)
        setConfirmOpen(true)
    }

    const handleConfirm = () => {
        if (!selectedReview) return
        
        if (confirmAction === 'approve') {
            approveMutation.mutate(selectedReview.id)
        } else {
            deleteMutation.mutate(selectedReview.id)
        }
    }

    // State cho menu actions
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null)
    const [menuRow, setMenuRow] = useState<ReviewResponseModel | null>(null)
    const openMenu = Boolean(anchorEl)

    const handleMenuClick = (event: React.MouseEvent<HTMLElement>, row: ReviewResponseModel) => {
        setAnchorEl(event.currentTarget)
        setMenuRow(row)
    }
    const handleMenuClose = () => {
        setAnchorEl(null)
        setMenuRow(null)
    }

    const columns = useMemo<GridColDef<ReviewResponseModel>[]>(
        () => [
            {
                field: 'type',
                headerName: 'Type',
                width: 110,
                renderCell: ({ value }) => (
                    <Chip
                        label={value === REVIEW_TYPE.Tag ? 'Tag' : 'Free'}
                        color={value === REVIEW_TYPE.Tag ? 'info' : 'default'}
                        size="small"
                    />
                ),
            },
            {
                field: 'tmdbMovieId',
                headerName: 'TMDB',
                width: 100,
            },
            {
                field: 'userName',
                headerName: 'User',
                flex: 1,
                minWidth: 160,
                valueGetter: (_value, row) => row.userName ?? `User #${row.userId}`,
            },
            {
                field: 'rating',
                headerName: 'Rating',
                width: 90,
            },
            {
                field: 'status',
                headerName: 'Status',
                width: 120,
                renderCell: ({ value }) => (
                    <Chip
                        label={REVIEW_STATUS_LABELS[value as ReviewStatus]}
                        color={value === REVIEW_STATUS.Pending ? 'warning' : value === REVIEW_STATUS.Deleted ? 'default' : 'success'}
                        size="small"
                    />
                ),
            },
            {
                field: 'createdOnUtc',
                headerName: 'Created',
                width: 170,
                valueFormatter: (value) => (value ? new Date(value).toLocaleString() : '—'),
            },
            {
                field: 'actions',
                headerName: 'Actions',
                width: 80,
                sortable: false,
                filterable: false,
                renderCell: ({ row }) => (
                    <>
                        <IconButton
                            aria-label="actions"
                            onClick={(e) => handleMenuClick(e, row)}
                            disabled={approveMutation.isPending || deleteMutation.isPending}
                        >
                            <MoreVertIcon />
                        </IconButton>
                    </>
                ),
            },
    // Menu popup cho actions
    // Menu nằm ngoài DataGrid để tránh render lại nhiều lần

        ],
    [approveMutation, deleteMutation],
    )

    const rows = (reviewsQuery.data?.items ?? []).map((review) => ({ ...review, id: review.id }))
    const totalRows = reviewsQuery.data?.totalCount ?? 0

    return (
        <PageContainer
            title="Reviews"
            subtitle="Monitor community sentiment and ensure high quality contributions."
            action={
                <Stack direction="row" spacing={1} flexWrap="wrap">
                    <ToggleButtonGroup
                        value={typeFilter}
                        exclusive
                        onChange={(_, value) => {
                            if (value === null) return
                            setTypeFilter(value as ReviewType | 'all')
                        }}
                        size="small"
                    >
                        <ToggleButton value={REVIEW_TYPE.Normal}>Standard</ToggleButton>
                        <ToggleButton value={REVIEW_TYPE.Tag}>Tag-based</ToggleButton>
                        <ToggleButton value="all">All types</ToggleButton>
                    </ToggleButtonGroup>
                    <ToggleButtonGroup
                        value={statusFilter}
                        exclusive
                        onChange={(_, value) => {
                            if (value === null) return
                            setStatusFilter(value as ReviewStatus | 'all')
                        }}
                        size="small"
                    >
                        <ToggleButton value="all">All</ToggleButton>
                        {Object.entries(REVIEW_STATUS_LABELS).map(([key, label]) => (
                            <ToggleButton key={key} value={Number(key)}>
                                {label}
                            </ToggleButton>
                        ))}
                    </ToggleButtonGroup>
                </Stack>
            }
        >
            <Stack spacing={2} mb={2}>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                    <TextField
                        label="Filter by email"
                        value={emailFilter}
                        onChange={(event) => setEmailFilter(event.target.value)}
                        size="small"
                        sx={{ flex: 1 }}
                    />
                    <TextField
                        label="Filter by TMDB id"
                        value={movieFilter}
                        onChange={(event) => setMovieFilter(event.target.value)}
                        type="number"
                        size="small"
                        sx={{ flex: 1 }}
                    />
                </Stack>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                    <TextField
                        label="Date from"
                        value={dateFrom}
                        onChange={(event) => setDateFrom(event.target.value)}
                        type="date"
                        size="small"
                        InputLabelProps={{ shrink: true }}
                        sx={{ flex: 1 }}
                    />
                    <TextField
                        label="Date to"
                        value={dateTo}
                        onChange={(event) => setDateTo(event.target.value)}
                        type="date"
                        size="small"
                        InputLabelProps={{ shrink: true }}
                        sx={{ flex: 1 }}
                    />
                </Stack>
            </Stack>

            <Box sx={{ height: 600 }}>
                <DataGrid
                    rows={rows}
                    columns={columns}
                    disableRowSelectionOnClick
                    paginationMode="server"
                    paginationModel={paginationModel}
                    onPaginationModelChange={setPaginationModel}
                    pageSizeOptions={[10, 25, 50]}
                    rowCount={totalRows}
                    loading={reviewsQuery.isLoading || reviewsQuery.isFetching}
                    slots={{ toolbar: StandardGridToolbar }}
                />
            </Box>

            {/* Menu Actions Popup */}
            <Menu
                anchorEl={anchorEl}
                open={openMenu}
                onClose={handleMenuClose}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                transformOrigin={{ vertical: 'top', horizontal: 'right' }}
            >
                <MenuItem
                    onClick={() => {
                        setSelectedReview(menuRow)
                        setDetailOpen(true)
                        handleMenuClose()
                    }}
                    disabled={approveMutation.isPending || deleteMutation.isPending}
                >
                    <VisibilityRoundedIcon fontSize="small" style={{ marginRight: 8 }} /> Xem chi tiết
                </MenuItem>
                {menuRow?.status === REVIEW_STATUS.Pending && (
                    <MenuItem
                        onClick={() => {
                            handleAction(menuRow, 'approve')
                            handleMenuClose()
                        }}
                        disabled={approveMutation.isPending || deleteMutation.isPending}
                        sx={{ color: 'success.main' }}
                    >
                        <CheckCircleRoundedIcon fontSize="small" style={{ marginRight: 8 }} /> Duyệt
                    </MenuItem>
                )}
                <MenuItem
                    onClick={() => {
                        if (menuRow) handleAction(menuRow, 'delete')
                        handleMenuClose()
                    }}
                    disabled={approveMutation.isPending || deleteMutation.isPending}
                    sx={{ color: 'error.main' }}
                >
                    <DeleteRoundedIcon fontSize="small" style={{ marginRight: 8 }} /> Xóa
                </MenuItem>
            </Menu>

            <ReviewDetailDialog open={detailOpen} review={selectedReview} onClose={() => setDetailOpen(false)} />
            
            <ConfirmDialog
                open={confirmOpen}
                title={confirmAction === 'approve' ? 'Approve review' : 'Delete review'}
                content={
                    confirmAction === 'approve'
                        ? `Are you sure you want to approve this review from "${selectedReview?.userName}"?`
                        : `Are you sure you want to delete this review from "${selectedReview?.userName}"?`
                }
                isLoading={approveMutation.isPending || deleteMutation.isPending}
                onClose={() => setConfirmOpen(false)}
                onConfirm={handleConfirm}
            />
        </PageContainer>
    )
}
