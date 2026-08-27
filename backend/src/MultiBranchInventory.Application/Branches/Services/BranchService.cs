using MultiBranchInventory.Application.Branches.DTOs;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Branches.Services;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _branchRepository;

    public BranchService(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<IReadOnlyList<BranchResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var branches = await _branchRepository.GetAllAsync(
            includeInactive,
            cancellationToken);

        return branches
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<BranchResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByIdAsync(
            id,
            cancellationToken);

        return branch is null
            ? null
            : MapToResponse(branch);
    }

    public async Task<BranchOperationResult> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code
            .Trim()
            .ToUpperInvariant();

        var codeExists = await _branchRepository.CodeExistsAsync(
            normalizedCode,
            cancellationToken: cancellationToken);

        if (codeExists)
        {
            return BranchOperationResult.Failure(
                "DUPLICATE_CODE",
                "A branch with this code already exists.");
        }

        var branch = new Branch
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Address = NormalizeOptional(request.Address),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Email = NormalizeEmail(request.Email),
            IsActive = true
        };

        await _branchRepository.AddAsync(
            branch,
            cancellationToken);

        await _branchRepository.SaveChangesAsync(
            cancellationToken);

        return BranchOperationResult.Success(
            MapToResponse(branch));
    }

    public async Task<BranchOperationResult> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (branch is null)
        {
            return BranchOperationResult.Failure(
                "NOT_FOUND",
                "Branch was not found.");
        }

        var normalizedCode = request.Code
            .Trim()
            .ToUpperInvariant();

        var codeExists = await _branchRepository.CodeExistsAsync(
            normalizedCode,
            id,
            cancellationToken);

        if (codeExists)
        {
            return BranchOperationResult.Failure(
                "DUPLICATE_CODE",
                "A branch with this code already exists.");
        }

        branch.Code = normalizedCode;
        branch.Name = request.Name.Trim();
        branch.Address = NormalizeOptional(request.Address);
        branch.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        branch.Email = NormalizeEmail(request.Email);
        branch.UpdatedAt = DateTime.UtcNow;

        await _branchRepository.SaveChangesAsync(
            cancellationToken);

        return BranchOperationResult.Success(
            MapToResponse(branch));
    }

    public async Task<BranchOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateBranchStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (branch is null)
        {
            return BranchOperationResult.Failure(
                "NOT_FOUND",
                "Branch was not found.");
        }

        branch.IsActive = request.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;

        await _branchRepository.SaveChangesAsync(
            cancellationToken);

        return BranchOperationResult.Success(
            MapToResponse(branch));
    }

    private static BranchResponse MapToResponse(Branch branch)
    {
        return new BranchResponse
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            Address = branch.Address,
            PhoneNumber = branch.PhoneNumber,
            Email = branch.Email,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToLowerInvariant();
    }
}