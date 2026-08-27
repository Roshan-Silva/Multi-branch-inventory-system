using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Application.Users.DTOs;
using MultiBranchInventory.Application.Users.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Users.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IBranchRepository branchRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _branchRepository = branchRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(
            includeInactive,
            cancellationToken);

        return users.Select(MapToResponse).ToList();
    }

    public async Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(
            id,
            cancellationToken);

        return user is null ? null : MapToResponse(user);
    }

    public async Task<UserOperationResult> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(
                normalizedEmail,
                cancellationToken: cancellationToken))
        {
            return DuplicateEmailFailure();
        }

        var branchValidation = await ValidateBranchAsync(
            request.Role,
            request.BranchId,
            cancellationToken);

        if (branchValidation.Failure is not null)
        {
            return branchValidation.Failure;
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            BranchId = branchValidation.Branch?.Id,
            Branch = branchValidation.Branch,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success(MapToResponse(user));
    }

    public async Task<UserOperationResult> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFoundFailure();
        }

        var normalizedEmail = NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(
                normalizedEmail,
                id,
                cancellationToken))
        {
            return DuplicateEmailFailure();
        }

        var branchValidation = await ValidateBranchAsync(
            request.Role,
            request.BranchId,
            cancellationToken);

        if (branchValidation.Failure is not null)
        {
            return branchValidation.Failure;
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = normalizedEmail;
        user.Role = request.Role;
        user.BranchId = branchValidation.Branch?.Id;
        user.Branch = branchValidation.Branch;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success(MapToResponse(user));
    }

    public async Task<UserOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFoundFailure();
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success(MapToResponse(user));
    }

    private async Task<(Branch? Branch, UserOperationResult? Failure)>
        ValidateBranchAsync(
            UserRole role,
            Guid? branchId,
            CancellationToken cancellationToken)
    {
        if (role == UserRole.SuperAdmin)
        {
            return branchId.HasValue
                ? (null, UserOperationResult.Failure(
                    "INVALID_BRANCH",
                    "SuperAdmin users cannot be assigned to a branch."))
                : (null, null);
        }

        if (!branchId.HasValue)
        {
            return (null, UserOperationResult.Failure(
                "BRANCH_REQUIRED",
                "A branch is required for this role."));
        }

        var branch = await _branchRepository.GetByIdAsync(
            branchId.Value,
            cancellationToken);

        if (branch is null)
        {
            return (null, UserOperationResult.Failure(
                "BRANCH_NOT_FOUND",
                "The selected branch was not found."));
        }

        if (!branch.IsActive)
        {
            return (null, UserOperationResult.Failure(
                "BRANCH_INACTIVE",
                "Users cannot be assigned to an inactive branch."));
        }

        return (branch, null);
    }

    private static UserResponse MapToResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            BranchId = user.BranchId,
            BranchName = user.Branch?.Name,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static UserOperationResult DuplicateEmailFailure()
    {
        return UserOperationResult.Failure(
            "DUPLICATE_EMAIL",
            "A user with this email already exists.");
    }

    private static UserOperationResult NotFoundFailure()
    {
        return UserOperationResult.Failure(
            "NOT_FOUND",
            "User was not found.");
    }
}
