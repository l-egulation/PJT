package com.hanjjak.account.application

import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character

data class AuthenticationResult(val account: Account, val character: Character)
