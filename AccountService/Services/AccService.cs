﻿using System;
using System.Threading.Tasks;
using AccountService.Domain;
using AccountService.Helpers;
using AccountService.Models;
using AccountService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AccountService.Services
{
    public class AccService : IAccService
    {
        //TODO Custom exception for every argument
        private readonly IAccountRepository _repository;
        private readonly IHasher _hasher;
        private readonly IRegexHelper _regexHelper = new RegexHelper();
        private readonly IJWTokenGenerator _tokenGenerator;

        public AccService(IAccountRepository repository, IHasher hasher, IJWTokenGenerator tokenGenerator)
        {
            _repository = repository;
            _hasher = hasher;
            _tokenGenerator = tokenGenerator;
        }

        public async Task<Account> CreateAccount(CreateAccountModel model)
        {
            var acc = await _repository.Get(model.Email);

            if (acc != null)
                throw new ArgumentException("Email is already in use.");
            
            if(!_regexHelper.IsValidEmail(model.Email))
                throw new ArgumentException("Email is not a valid email.");
            
            if (!_regexHelper.IsValidPassword(model.Password))
                throw new ArgumentException("Password doesn't meet the requirements.");
            
            //hash the password. 
            var salt = _hasher.CreateSalt();
            var hashedPassword = await _hasher.HashPassword(model.Password, salt);
            
            //Create new User object and send to repository
            var newAccount = new Account()
            {
                Id = Guid.NewGuid(),
                Email = model.Email,
                Password = hashedPassword,
                Salt = salt
            };

            await _repository.Create(newAccount);
            
            return newAccount.WithoutPassword();
        }


        public async Task<Account> Login(LoginModel loginModel)
        {
            var account = _repository.Get(loginModel.Email).Result;
            if (account == null) throw new ArgumentException("A user with this email address does not exist. ");

            if (!await _hasher.VerifyHash(loginModel.Password, account.Salt, account.Password))
            {
                throw new ArgumentException("The password is incorrect.");
            }

            account.Token = _tokenGenerator.GenerateJWT(account.Id);
            return account.WithoutPassword();
        }
        
        public async Task<Account> UpdatePassword(Guid id, ChangePasswordModel passwordModel)
        {
            var account = await GetAccount(id);
            
            if (!await _hasher.VerifyHash(passwordModel.OldPassword, account.Salt, account.Password))
            {
                throw new ArgumentException("The password is incorrect.");
            }

            if (_regexHelper.IsValidPassword(passwordModel.NewPassword))
            {
                //hash the password. 
                var salt = _hasher.CreateSalt();
                var hashedPassword = await _hasher.HashPassword(passwordModel.NewPassword, salt);
                account.Salt = salt;
                account.Password = hashedPassword;
            }
            var updatedAccount = await _repository.Update(account.Id, account);
            return updatedAccount.WithoutPassword();
        }

        public async Task<Account> UpdateAccount(Guid id, UpdateAccountModel model)
        {
            if (_regexHelper.IsValidEmail(model.Email)) return null;

            var acc = await GetAccount(id);
            acc.Email = model.Email;
            acc.isDelegate = model.isDelegate;
            acc.isDAppOwner = model.isDelegate;
            
            
            var updatedAccount = await _repository.Update(id, acc);
            return updatedAccount.WithoutPassword();
        }

        public async Task<Account> GetAccount(Guid id)
        {
            var acc = await _repository.Get(id);
            return acc.WithoutPassword();
        }

        public async Task DeleteAccount(Guid id)
        {
            if (await _repository.Get(id) != null)
            {
                await _repository.Remove(id);
            }
            
        }
    }
}